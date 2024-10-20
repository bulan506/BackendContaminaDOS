using System;
using System.Linq;
using System.Collections.Generic;
using Core.Models.Data;
using Core.Models.Types;
using MongoDB.Driver;

namespace Core.Models.Business
{
    public class GameService : IGameService
    {
        private readonly IMongoCollection<Game> _gamesCollection;
        private readonly IMongoCollection<Round> _roundsCollection;
        private readonly IMongoCollection<GroupRound> _groupRoundsCollection;


        public GameService(MongoDbSettings settings)
        {
            var client = new MongoClient(settings.ConnectionString);
            var database = client.GetDatabase(settings.DatabaseName);
            _gamesCollection = database.GetCollection<Game>(settings.GamesCollectionName);
            _roundsCollection = database.GetCollection<Round>(settings.RoundsCollectionName);
            _groupRoundsCollection = database.GetCollection<GroupRound>("GroupRounds");
        }

        public ResponseJoin JoinGame(string gameId, string player, string password = null)
        {
            var game = _gamesCollection.Find(g => g.GameId == gameId).SingleOrDefault();

            if (game == null)
            {
                return new ResponseJoin
                {
                    status = 404,
                    msg = "Game not found",
                    data = null
                };
            }

            // Verificar si el número máximo de jugadores ya ha sido alcanzado
            if (game.Players.Count >= 10)
            {
                return new ResponseJoin
                {
                    status = 409, // Conflicto, el límite de jugadores ha sido alcanzado
                    msg = "The game already has the maximum number of players",
                    data = null
                };
            }

            // Verificar si se requiere una contraseña
            if (!string.IsNullOrEmpty(game.GamePassword))
            {
                // Si no se proporciona una contraseña
                if (string.IsNullOrEmpty(password))
                {
                    return new ResponseJoin
                    {
                        status = 401, // No autorizado
                        msg = "Invalid Credentials",
                        data = null
                    };
                }

                // Si la contraseña proporcionada no coincide con la del juego
                if (game.GamePassword.Trim() != password.Trim())
                {
                    return new ResponseJoin
                    {
                        status = 401, // No autorizado
                        msg = "Invalid Credentials",
                        data = null
                    };
                }
            }

            // Verificar si el jugador ya está en el juego
            var existingPlayer = game.Players.FirstOrDefault(p => p.PlayerName == player);
            if (existingPlayer != null)
            {
                return new ResponseJoin
                {
                    status = 409, // Conflicto, jugador ya está en el juego
                    msg = "Player is already part of the game",
                    data = null
                };
            }
            // Si todo es correcto, añadimos el jugador al juego
            var newPlayer = new Player
            {
                PlayerId = Guid.NewGuid().ToString(),
                PlayerName = player,
                PlayerType = "participant" // Asignar tipo de jugador
            };

            game.Players.Add(newPlayer);
            game.UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // Actualizamos el juego en la base de datos
            var update = Builders<Game>.Update
                .Set(g => g.Players, game.Players)
                .Set(g => g.UpdatedAt, game.UpdatedAt);

            _gamesCollection.UpdateOne(g => g.GameId == gameId, update);

            var data = new GameSearch
            {
                id = game.GameId,
                name = game.GameName,
                owner = game.GameOwner,
                status = game.GameStatus.ToString(),
                password = !string.IsNullOrEmpty(game.GamePassword),
                currentRound = game.CurrentRound,
                createdAt = game.CreatedAt,
                updatedAt = game.UpdatedAt,
                players = game.Players.Select(p => p.PlayerName).ToList(),
                enemies = game.Players.Where(p => p.PlayerType == "enemy").Select(p => p.PlayerName).ToList()
            };

            return new ResponseJoin
            {
                status = 200,
                msg = "Player joined the game",
                data = data
            };
        }

        // Agregar los nuevos métodos
        public void AddGroupRound(GroupRound groupRound)
        {
            if (groupRound == null)
                throw new ArgumentNullException(nameof(groupRound));

            // Verificar que existan las referencias
            var game = _gamesCollection.Find(g => g.GameId == groupRound.gameId).FirstOrDefault();
            var round = _roundsCollection.Find(r => r.id == groupRound.roundId).FirstOrDefault();
            var player = game?.Players.FirstOrDefault(p => p.PlayerName == groupRound.playerId);

            if (game == null || round == null || player == null)
            {
                throw new InvalidOperationException("Invalid references in GroupRound");
            }

            // Asignar las referencias virtuales
            groupRound.game = game;
            groupRound.round = round;
            groupRound.player = player;

            _groupRoundsCollection.InsertOne(groupRound);
        }

        public void UpdateRound(DataRounds round)
        {
            if (round == null)
                throw new ArgumentNullException(nameof(round));

            var filter = Builders<Round>.Filter.Eq(r => r.id, round.id);
            var update = Builders<Round>.Update
                .Set(r => r.status, round.status)
                .Set(r => r.updatedAt, round.updatedAt)
                .Set(r => r.group, round.group)
                .Set(r => r.votes, round.votes);

            _roundsCollection.UpdateOne(filter, update);
        }


        public ResponseStart StartGame(string gameId, string player, string password)
        {
            var game = _gamesCollection.Find(g => g.GameId == gameId).SingleOrDefault();

            if (game == null)
            {
                return new ResponseStart { status = 404 };
            }
            // Verificar que el jugador que hace la solicitud sea el owner
            var owner = game.Players.FirstOrDefault(p => p.PlayerType == "owner");
            if (owner == null || owner.PlayerName != player)
            {
                return new ResponseStart { status = 401 };
            }

            if (!string.IsNullOrEmpty(game.GamePassword) && game.GamePassword != password)
            {
                return new ResponseStart { status = 401 };
            }

            // Verificar si el juego ya está en progreso
            if (game.GameStatus == GameStatus.rounds)
            {
                return new ResponseStart { status = 409 };
            }

            // Verificar si hay suficientes jugadores
            if (game.Players.Count < 5)
            {
                return new ResponseStart { status = 428 };
            }

            // Iniciar el juego
            game.GameStatus = GameStatus.rounds; // Marca el juego como en curso
            game.UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            AssignRoles(game);
            var firstRound = CreateNewRound(game);
            game.CurrentRound = firstRound.id;
            _gamesCollection.ReplaceOne(g => g.GameId == gameId, game);
            _roundsCollection.InsertOne(firstRound);
            return new ResponseStart { status = 200 };
        }

        private void AssignRoles(Game game)
        {
            var playerCount = game.Players.Count;
            var enemyCount = playerCount switch
            {
                <= 6 => 2,
                <= 9 => 3,
                _ => 4
            };

            var shuffledPlayers = game.Players.OrderBy(x => Guid.NewGuid()).ToList();
            for (int i = 0; i < shuffledPlayers.Count; i++)
            {
                shuffledPlayers[i].PlayerRole = i < enemyCount ? "enemy" : "citizen";
            }
            game.Players = shuffledPlayers;
        }

        private Round CreateNewRound(Game game)
        {
            var leader = game.Players[new Random().Next(game.Players.Count)];
            return new Round
            {
                status = "waiting-on-leader",
                phase = "vote1",
                result = "none",
                leader = leader.PlayerName,
                createdAt = DateTime.UtcNow,
                updatedAt = DateTime.UtcNow,
                gameId = game.GameId,
                group = new List<string>(),
                votes = new List<bool>(),
                id = Guid.NewGuid().ToString()
            };

        }
        public RoundsResponse GetRounds(string gameId, string player, string password)
        {

            var game = _gamesCollection.Find(g => g.GameId == gameId).SingleOrDefault();
            if (game == null)
            {
                return new RoundsResponse { status = 404, msg = "The specified resource was not found" };
            }

            if (!game.Players.Any(p => p.PlayerName == player))
            {
                return new RoundsResponse { status = 403, msg = "Not part of the game" };
            }

            // Validación de contraseña solo si el juego tiene contraseña y el jugador proporciona una
            if (!string.IsNullOrEmpty(game.GamePassword) && !string.IsNullOrEmpty(password))
            {
                if (game.GamePassword != password)
                {
                    return new RoundsResponse { status = 401, msg = "Invalid credentials" };
                }
            }
            // Verificar si el juego está en estado "lobby"
            if (game.GameStatus == GameStatus.lobby)
            {
                return new RoundsResponse
                {
                    status = 200,
                    msg = "Results found",
                    data = new List<DataRounds>(),
                    others = new List<ErrorDetail>()
                };
            }

            var rounds = _roundsCollection
                .Find(r => r.gameId == gameId)
                .SortByDescending(r => r.createdAt)
                .ToList();

            var dataRounds = rounds.Select(r => new DataRounds
            {
                id = r.id,
                leader = r.leader,
                status = r.status,
                result = r.result,
                phase = r.phase,
                group = r.group,
                votes = r.votes,
                createdAt = r.createdAt,
                updatedAt = r.updatedAt
            }).ToList();

            return new RoundsResponse
            {
                status = 200,
                msg = "Results found",
                data = dataRounds,
                others = new List<ErrorDetail>()
            };
        }

        public SRoundsResponse GetRoundDetail(string gameId, string roundId, string player, string password)
        {
            var game = _gamesCollection.Find(g => g.GameId == gameId).SingleOrDefault();
            if (game == null)
            {
                return new SRoundsResponse
                {
                    status = 404,
                    msg = "The specified resource was not found"
                };
            }

            if (!game.Players.Any(p => p.PlayerName == player))
            {
                return new SRoundsResponse
                {
                    status = 403,
                    msg = "Not part of the game"
                };
            }

            // Validación de contraseña
            if (!string.IsNullOrEmpty(game.GamePassword))
            {
                if (string.IsNullOrEmpty(password) || game.GamePassword != password)
                {
                    return new SRoundsResponse
                    {
                        status = 401,
                        msg = "Invalid credentials"
                    };
                }
            }

            var round = _roundsCollection.Find(r => r.gameId == gameId && r.id == roundId).SingleOrDefault();
            if (round == null)
            {
                return new SRoundsResponse
                {
                    status = 404,
                    msg = "Invalid Round Id",
                    data = { },
                    others = []
                };
            }

            var dataRound = new DataRounds
            {
                id = round.id,
                leader = round.leader,
                status = round.status,
                result = round.result,
                phase = round.phase,
                group = round.group,
                votes = round.votes,
                createdAt = round.createdAt,
                updatedAt = round.updatedAt
            };

            return new SRoundsResponse
            {
                status = 200,
                msg = "Round found",
                data = dataRound
            };
        }


        public SRoundsResponse GetRoundById(string gameId, string roundId)
        {
            // Buscar el juego utilizando el gameId
            var game = _gamesCollection.Find(g => g.GameId == gameId).SingleOrDefault();

            if (game == null)
            {
                return new SRoundsResponse
                {
                    status = 404,
                    msg = "Game not found",
                    data = null
                };
            }

            // Aquí puedes buscar la ronda específica usando roundId
            var round = _roundsCollection.Find(r => r.gameId == gameId && r.id == roundId).SingleOrDefault();

            if (round == null)
            {
                return new SRoundsResponse
                {
                    status = 404,
                    msg = "Round not found",
                    data = null
                };
            }

            // Si se encuentra la ronda, se devuelve la información
            var dataRound = new DataRounds
            {
                id = round.id,
                leader = round.leader,
                status = round.status,
                result = round.result,
                phase = round.phase,
                group = round.group,
                votes = round.votes,
                createdAt = round.createdAt,
                updatedAt = round.updatedAt
            };

            return new SRoundsResponse
            {
                status = 200,
                msg = "Round found",
                data = dataRound
            };
        }


        public ResponseGameId GetGameById(string gameId, string player, string password)
        {
            var game = _gamesCollection.Find(g => g.GameId == gameId).SingleOrDefault();

            if (game == null)
            {
                return new ResponseGameId
                {
                    status = 404,
                    msg = "Game not found",
                    data = null
                };
            }

            if (!game.Players.Any(p => p.PlayerName == player))
            {
                return new ResponseGameId
                {
                    status = 403,
                    msg = "Player not part of the game",
                    data = null
                };
            }

            if (!string.IsNullOrEmpty(game.GamePassword) && game.GamePassword != password)
            {
                return new ResponseGameId
                {
                    status = 401,
                    msg = "Invalid credentials",
                    data = null
                };
            }

            // Mapeo a DataCreate
            var data = new DataCreate
            {
                id = game.GameId, // Cambia GameId por id
                name = game.GameName, // Cambia GameName por name
                owner = game.GameOwner, // Asigna el propietario
                status = game.GameStatus.ToString(), // Convierte el estado a string
                password = !string.IsNullOrEmpty(game.GamePassword), // Indica si hay contraseña
                currentRound = game.CurrentRound,
                createdAt = game.CreatedAt,
                updatedAt = game.UpdatedAt,
                players = game.Players.Select(p => p.PlayerName).ToList(), // Lista de jugadores
                enemies = game.Enemies // Asigna enemigos, si corresponde
            };

            return new ResponseGameId
            {
                status = 200,
                msg = "Game found",
                data = data
            };
        }

        public bool HasGroupAlreadyProposed(string roundId, string phase)
        {
            // Verificar si ya se ha propuesto un grupo en esa fase
            return _groupRoundsCollection
            .Find(g => g.roundId == roundId)
            .Any();


        }


      

        // Método para proponer un grupo
        public SRoundsResponse ProposeGroup(string gameId, string roundId, GroupRequest groupRequest, string password, string player)
        {
            // Validación del jugador
            if (string.IsNullOrEmpty(player) || player.Length < 3 || player.Length > 20)
            {
                return new SRoundsResponse
                {
                    status = 400,
                    msg = "Invalid or missing player name",
                    data = null,
                    others = new List<ErrorDetail>
                {
                    new ErrorDetail { status = 400, msg = "Invalid or missing player name" }
                }
                };
            }

            // Validación del grupo
            if (groupRequest?.group == null || !groupRequest.group.Any())
            {
                return new SRoundsResponse
                {
                    status = 400,
                    msg = "Invalid or missing group",
                    data = null,
                    others = new List<ErrorDetail>
                {
                    new ErrorDetail { status = 400, msg = "Invalid or missing group" }
                }
                };
            }

            // Verificar existencia del juego
            var gameResponse = GetGameById(gameId, player, password);
            if (gameResponse.status == 404)
            {
                return new SRoundsResponse
                {
                    status = 404,
                    msg = "Game not found",
                    data = null,
                    others = new List<ErrorDetail>
                {
                    new ErrorDetail { status = 404, msg = "Game not found" }
                }
                };
            }

            // Obtener detalles de la ronda
            var roundResponse = GetRoundDetail(gameId, roundId, player, password);
            if (roundResponse.status != 200 || roundResponse.data == null)
            {
                return roundResponse;
            }

            var round = roundResponse.data;

            // Validar estado de la ronda y líder
            if (round.status != "waiting-on-leader" || player != round.leader)
            {
                return new SRoundsResponse
                {
                    status = 428,
                    msg = "This action is not allowed at this time",
                    data = null
                };
            }

            // Verificar si ya se propuso un grupo
            if (HasGroupAlreadyProposed(round.id, round.phase) && round.status != "voting")
            {
                return new SRoundsResponse
                {
                    status = 409,
                    msg = "Group already proposed in this phase",
                    data = null
                };
            }

            // Validar tamaño del grupo
            int requiredGroupSize = GetRequiredGroupSize(gameResponse.data.players.Count());
            if (groupRequest.group.Count != requiredGroupSize)
            {
                return new SRoundsResponse
                {
                    status = 428,
                    msg = $"Requires a group of {requiredGroupSize} members",
                    data = null
                };
            }

            // Validar que todos los miembros del grupo existan
            var invalidPlayers = groupRequest.group.Where(member => !gameResponse.data.players.Contains(member)).ToList();
            if (invalidPlayers.Any())
            {
                return new SRoundsResponse
                {
                    status = 428,
                    msg = $"The player(s) {string.Join(", ", invalidPlayers)} don't exist",
                    data = null
                };
            }

            try
            {
                // Registrar miembros del grupo
                foreach (var member in groupRequest.group)
                {
                    var groupRound = new GroupRound
                    {
                        gameId = gameResponse.data.id,
                        roundId = round.id,
                        playerId = member
                    };
                    AddGroupRound(groupRound);
                }

                // Actualizar estado de la ronda
                round.status = "voting";
                round.updatedAt = DateTime.UtcNow;
                UpdateRound(round); // Asegurarse de que UpdateRound acepte un objeto Round

                // Obtener la ronda actualizada para la respuesta
                var updatedRoundResponse = GetRoundDetail(gameId, roundId, player, password);
                return updatedRoundResponse;
            }
            catch (Exception)
            {
                return new SRoundsResponse
                {
                    status = 500,
                    msg = "An error occurred while processing the request",
                    data = null,
                    others = new List<ErrorDetail>
                {
                    new ErrorDetail { status = 500, msg = "Internal server error" }
                }
                };
            }
        }

//FALTA MODIFICAR ESTO
        private int GetRequiredGroupSize(int playerCount)
        {
            return Math.Max(3, playerCount / 3);
        }

    

    }
}
