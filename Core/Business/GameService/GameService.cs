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


        public GameService(MongoDbSettings settings)
        {
            var client = new MongoClient(settings.ConnectionString);
            var database = client.GetDatabase(settings.DatabaseName);
            _gamesCollection = database.GetCollection<Game>(settings.GamesCollectionName);
            _roundsCollection = database.GetCollection<Round>(settings.RoundsCollectionName);
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

    }
}
