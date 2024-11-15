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
        private static readonly int MINIMUM_PLAYERS = 5;
        private static readonly int MAXIMUM_PLAYERS = 10;
        private static readonly Random _random = new Random();
        private static readonly int[,] GROUP_SIZES = {
    // Round 1, 2, 3, 4, 5
    { 2, 3, 2, 3, 3 }, // 5 players
    { 2, 3, 4, 3, 4 }, // 6 players
    { 2, 3, 3, 4, 4 }, // 7 players
    { 3, 4, 4, 5, 5 }, // 8 players
    { 3, 4, 4, 5, 5 }, // 9 players
    { 3, 4, 4, 5, 5 }  // 10 players
};

        public GameService(DbSettings settings)
        {
            var client = new MongoClient(settings.ConnectionString);
            var database = client.GetDatabase(settings.DatabaseName);
            _gamesCollection = database.GetCollection<Game>(settings.GamesCollectionName);
            _roundsCollection = database.GetCollection<Round>(settings.RoundsCollectionName);
        }

        public async Task<ResponseJoin> JoinGameAsync(string gameId, string player, string password = null)
        {
            var game = await _gamesCollection.Find(g => g.GameId == gameId).SingleOrDefaultAsync();

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
            // Verificar si el juego ya empezo
            if (game.GameStatus != GameStatus.lobby)
            {
                return new ResponseJoin
                {
                    status = 409, // Conflicto, el límite de jugadores ha sido alcanzado
                    msg = "The game already started",
                    data = { }
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
                PlayerType = "participant", // Asignar tipo de jugador
                PlayerVote = "none",
                PlayerAction = "none"
            };

            game.Players.Add(newPlayer);
            game.UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // Actualizamos el juego en la base de datos
            var update = Builders<Game>.Update
                .Set(g => g.Players, game.Players)
                .Set(g => g.UpdatedAt, game.UpdatedAt);

            await _gamesCollection.UpdateOneAsync(g => g.GameId == gameId, update);

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


        public async Task<ResponseStart> StartGameAsync(string gameId, string player, string password)
        {
            var game = await _gamesCollection.Find(g => g.GameId == gameId).SingleOrDefaultAsync();

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
            var firstRound = await CreateNewRoundAsync(game, 0);
            game.CurrentRound = firstRound.id;
            await _gamesCollection.ReplaceOneAsync(g => g.GameId == gameId, game);
            return new ResponseStart { status = 200 };
        }

        private void AssignRoles(Game game)
        {
            // Determinar número de enemigos según cantidad de jugadores
            var enemyCount = game.Players.Count switch
            {
                <= 6 => 2,
                <= 9 => 3,
                _ => 4
            };

            // Asignar roles aleatoriamente
            int enemiesAssigned = 0;
            while (enemiesAssigned < enemyCount)
            {
                // Elegir un índice aleatorio entre 0 y el número total de jugadores - 1
                int randomIndex = _random.Next(game.Players.Count);
                // Si el jugador elegido no es enemigo, convertirlo
                if (game.Players[randomIndex].PlayerRole != "enemy")
                {
                    game.Players[randomIndex].PlayerRole = "enemy";
                    enemiesAssigned++;
                }
            }

            // Asignar rol de "citizen" a todos los que no sean enemigos
            foreach (var player in game.Players.Where(p => p.PlayerRole != "enemy"))
            {
                player.PlayerRole = "citizen";
            }
        }

        private async Task ResetPlayerVotesAsync(Game game)
        {
            foreach (var player in game.Players)
            {
                player.PlayerVote = "none";
                player.PlayerAction = "none";
            }
            await _gamesCollection.ReplaceOneAsync(g => g.GameId == game.GameId, game);
        }
        private async Task<Round> CreateNewRoundAsync(Game game, int currentRoundNumber)
        {
            if (currentRoundNumber > 0)
            {
                await ResetPlayerVotesAsync(game);
            }
            var leader = game.Players[new Random().Next(game.Players.Count)];
            var newRound = new Round
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
                actions = new List<bool>(),
                id = Guid.NewGuid().ToString(),
                roundCount = currentRoundNumber + 1
            };
            await _roundsCollection.InsertOneAsync(newRound);
            return newRound;

        }
        public async Task<RoundsResponse> GetRoundsAsync(string gameId, string player, string password)
        {

            var game = await _gamesCollection.Find(g => g.GameId == gameId).SingleOrDefaultAsync();
            if (game == null)
            {
                return new RoundsResponse { status = 404, msg = "The specified resource was not found" };
            }

            if (!game.Players.Any(p => p.PlayerName == player))
            {
                return new RoundsResponse { status = 403, msg = "Not part of the game" };
            }

            // Validación de contraseña
            if (!string.IsNullOrEmpty(game.GamePassword))
            {
                if (string.IsNullOrEmpty(password) || game.GamePassword != password)
                {
                    return new RoundsResponse
                    {
                        status = 401,
                        msg = "Invalid credentials"
                    };
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

            var rounds = await _roundsCollection
                .Find(r => r.gameId == gameId)
                .SortByDescending(r => r.createdAt)
                .ToListAsync();

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

        public async Task<SRoundsResponse> GetRoundDetailAsync(string gameId, string roundId, string player, string password)
        {
            var game = await _gamesCollection.Find(g => g.GameId == gameId).SingleOrDefaultAsync();
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

            var round = await _roundsCollection.Find(r => r.gameId == gameId && r.id == roundId).SingleOrDefaultAsync();
            if (round == null)
            {
                return new SRoundsResponse
                {
                    status = 404,
                    msg = "The specified round was not found",
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
                msg = "Results found",
                data = dataRound
            };
        }


        private async Task<ResponseGameId> GetGameByIdAsync(string gameId, string player, string password)
        {
            var game = await _gamesCollection.Find(g => g.GameId == gameId).SingleOrDefaultAsync();

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


        // Método para proponer un grupo
        public async Task<SRoundsResponse> ProposeGroupAsync(string gameId, string roundId, GroupRequest groupRequest, string password, string player)
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
            var gameResponse = await GetGameByIdAsync(gameId, player, password);
            if (gameResponse.status != 200 || gameResponse.data == null)
            {
                return new SRoundsResponse
                {
                    status = gameResponse.status,
                    msg = gameResponse.msg,
                    data = null,
                    others = new List<ErrorDetail>
            {
                new ErrorDetail { status = gameResponse.status, msg = gameResponse.msg }
            }
                };
            }

            // Obtener detalles de la ronda
            var roundResponse = await GetRoundDetailAsync(gameId, roundId, player, password);
            if (roundResponse.status != 200 || roundResponse.data == null)
            {
                return roundResponse;
            }

            var round = roundResponse.data;

            // Validar estado de la ronda y líder
            if (round.status != "waiting-on-leader")
            {
                return new SRoundsResponse
                {
                    status = 428,
                    msg = "This action is not allowed at this time",
                    data = null
                };
            }

            // Verificar si ya se propuso un grupo
            if (round.group != null && round.group.Any())
            {
                return new SRoundsResponse
                {
                    status = 409,
                    msg = "Asset already exists",
                    data = null
                };
            }

            var roundBD = await _roundsCollection.Find(r => r.gameId == gameId && r.id == round.id).SingleOrDefaultAsync();
            int requiredGroupSize = GetRequiredGroupSize(gameResponse.data.players.Count(), roundBD.roundCount);
            if (requiredGroupSize < 0)
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
            if (groupRequest.group.Count != requiredGroupSize)
            {
                return new SRoundsResponse
                {
                    status = 428,
                    msg = $"Requires a group of {requiredGroupSize} members",
                    data = null
                };
            }
            var distinctMembers = groupRequest.group.Distinct().ToList();
            if (distinctMembers.Count != groupRequest.group.Count)
            {
                return new SRoundsResponse
                {
                    status = 428,
                    msg = "Invalid or missing group",
                    data = null,
                    others = new List<ErrorDetail>
        {
            new ErrorDetail { status = 428, msg = "Group members must be different from each other" }
        }
                };
            }

            // Validar que todos los miembros del grupo existan
            var invalidPlayers = groupRequest.group.Where(member => !gameResponse.data.players.Contains(member)).ToList();
            if (invalidPlayers.Any())
            {
                return new SRoundsResponse
                {
                    status = 409,
                    msg = $"Invalid group members",
                    data = null
                };
            }
            // llegar aqui ya paso por todos los filtros
            return await AddGroupRounAsync(gameId, roundId, groupRequest, player, password);
        }
        private async Task<SRoundsResponse> AddGroupRounAsync(string gameId, string roundId, GroupRequest groupRequest, string player, string password)
        {
            try
            {
                // Obtener la ronda actual
                var round = await _roundsCollection.Find(r => r.id == roundId && r.gameId == gameId).FirstOrDefaultAsync();
                if (round == null)
                {
                    return new SRoundsResponse
                    {
                        status = 404,
                        msg = "Round not found",
                        data = null,
                        others = new List<ErrorDetail>
                {
                    new ErrorDetail { status = 404, msg = "Round not found" }
                }
                    };
                }

                // Validar que el jugador sea el líder de la ronda
                if (round.leader != player)
                {
                    return new SRoundsResponse
                    {
                        status = 409,
                        msg = "Only the round leader can propose groups",
                        data = null,
                        others = new List<ErrorDetail>
                {
                    new ErrorDetail { status = 403, msg = "Only the round leader can propose groups" }
                }
                    };
                }

                // Validar que la ronda esté en el estado correcto
                if (round.status != "waiting-on-leader")
                {
                    return new SRoundsResponse
                    {
                        status = 409,
                        msg = "Asset already exists",
                        data = null,
                        others = new List<ErrorDetail>
                {
                    new ErrorDetail { status = 409, msg = "Round is not in the correct state for group proposal" }
                }
                    };
                }
                // Definir la actualización
                var update = Builders<Round>.Update
                    .Set(r => r.group, groupRequest.group)
                    .Set(r => r.status, "voting")
                    .Set(r => r.votes, new List<bool>())  // Inicializar los votos
                    .Set(r => r.updatedAt, DateTime.UtcNow);

                // Ejecutar la actualización
                var updateResult = await _roundsCollection.UpdateOneAsync(
                    r => r.id == roundId && r.gameId == gameId,
                    update
                );

                if (updateResult.ModifiedCount > 0)
                {
                    return await GetRoundDetailAsync(gameId, roundId, player, password);
                }
                else
                {
                    return new SRoundsResponse
                    {
                        status = 500,
                        msg = "Failed to update round",
                        data = null,
                        others = new List<ErrorDetail>
                {
                    new ErrorDetail { status = 500, msg = "Failed to update the round in the database" }
                }
                    };
                }
            }
            catch (Exception ex)
            {
                return new SRoundsResponse
                {
                    status = 500,
                    msg = "An error occurred while updating the round",
                    data = null,
                    others = new List<ErrorDetail>
            {
                new ErrorDetail { status = 500, msg = ex.Message }
            }
                };
            }
        }

        private int GetRequiredGroupSize(int playerCount, int currentRound)
        {
            // Validar el número de jugadores y si hay algo mal entonces -1
            if (playerCount < MINIMUM_PLAYERS || playerCount > MAXIMUM_PLAYERS)
            {
                return -1;
            }

            // Validar el número de ronda (las rondas van de 1 a 5)
            if (currentRound < 1 || currentRound > 5)
            {
                return -1;
            }

            int playerIndex = playerCount - MINIMUM_PLAYERS;
            int roundIndex = currentRound - 1;

            return GROUP_SIZES[playerIndex, roundIndex];
        }


        public async Task<SRoundsResponse> VoteAsync(string gameId, string roundId, string player, string password, bool vote)
        {
            var game = await _gamesCollection.Find(g => g.GameId == gameId).SingleOrDefaultAsync();
            if (game == null)
            {
                return new SRoundsResponse { status = 404, msg = "The specified resource was not found" };
            }
            if (game.GameStatus == GameStatus.ended || game.GameStatus == GameStatus.lobby)
            {
                return new SRoundsResponse
                {
                    status = 428,
                    msg = "This action is not allowed at this time",
                    data = null
                };
            }

            var currentPlayer = game.Players.FirstOrDefault(p => p.PlayerName == player);
            if (currentPlayer == null)
            {
                return new SRoundsResponse { status = 403, msg = "Not part of the game" };
            }

            // Validación de si el jugador ya votó
            if (currentPlayer.PlayerVote != "none")
            {  // Asumiendo que 'none' es un string que representa que no ha votado
                return new SRoundsResponse { status = 409, msg = "Player has already voted", data = null };
            }

            // Validación de contraseña
            if (!string.IsNullOrEmpty(game.GamePassword))
            {
                if (string.IsNullOrEmpty(password) || game.GamePassword != password)
                {
                    return new SRoundsResponse { status = 401, msg = "Invalid credentials" };
                }

            }

            var round = await _roundsCollection.Find(r => r.gameId == gameId && r.id == roundId).SingleOrDefaultAsync();

            if (game.GameStatus != GameStatus.rounds || round.status != "voting")
            {
                return new SRoundsResponse { status = 428, msg = "This action is not allowed at this time", data = null };
            }

            if (round == null)
            {
                return new SRoundsResponse { status = 404, msg = "Round not found", data = null };
            }

            if (round.votes.Count >= game.Players.Count)
            {
                return new SRoundsResponse { status = 409, msg = "Votes already cast", data = null };
            }

            currentPlayer.PlayerVote = vote.ToString();

            var playerIndex = game.Players.FindIndex(p => p.PlayerName == player);
            if (playerIndex != -1)
            {
                game.Players[playerIndex] = currentPlayer;
            }

            await _gamesCollection.ReplaceOneAsync(game => game.GameId == gameId, game);
            round.votes.Add(vote);

            // Si ya han votado todos los jugadores, determinar el resultado
            if (round.votes.Count >= game.Players.Count)
            {
                var resultRound = await determineResultVotingAsync(round, game);
                game.Players.ForEach(p => p.PlayerVote = "none");
                await _gamesCollection.ReplaceOneAsync(game => game.GameId == gameId, game);
                await _roundsCollection.ReplaceOneAsync(r => r.id == roundId, resultRound);
            }
            else
            {
                await _roundsCollection.ReplaceOneAsync(r => r.id == roundId, round);
            }

            round = await _roundsCollection.Find(r => r.gameId == gameId && r.id == roundId).SingleOrDefaultAsync();

            var dataRound = new DataRounds
            {
                id = round.id,
                leader = round.leader,
                status = round.status,
                result = round.result,
                phase = round.phase,
                group = round.group,
                votes = round.votes.Select(v => (bool)v).ToList(),
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
        private async Task<Round> determineResultVotingAsync(Round round, Game game)
        {
            var votesTrue = round.votes.Count(v => v == true);
            var votesFalse = round.votes.Count(v => v == false);

            if (votesTrue == votesFalse || votesFalse > votesTrue)
            {
                round.status = "waiting-on-leader";
                switch (round.phase)
                {
                    case "vote1":
                        round.votes = new List<bool>();
                        round.group = new List<string>();
                        round.phase = "vote2";
                        break;
                    case "vote2":
                        round.votes = new List<bool>();
                        round.group = new List<string>();
                        round.phase = "vote3";
                        break;
                    case "vote3":
                        round.status = "ended";
                        round.result = "enemies";
                        await _roundsCollection.ReplaceOneAsync(r => r.id == round.id && r.gameId == game.GameId, round);

                        if (await CheckGameEndConditionAsync(game, round.result))
                        {
                            await EndGameAsync(game, round.result, round);
                        }
                        else
                        {
                            var newRound = await CreateNewRoundAsync(game, round.roundCount);
                            game.CurrentRound = newRound.id;
                            await _gamesCollection.ReplaceOneAsync(g => g.GameId == game.GameId, game);
                        }
                        break;
                }
            }
            else
            {
                round.status = "waiting-on-group";
            }
            round.updatedAt = DateTime.UtcNow;
            await _roundsCollection.ReplaceOneAsync(r => r.id == round.id && r.gameId == game.GameId, round);

            return round;
        }

        public async Task<SRoundsResponse> SubmitActionAsync(string gameId, string roundId, string player, string password, bool action)
        {
            // Verificaciones iniciales
            var game = await _gamesCollection.Find(g => g.GameId == gameId).SingleOrDefaultAsync();
            if (game == null)
            {
                return new SRoundsResponse
                {
                    status = 404,
                    msg = "The specified game was not found",
                    data = null
                };
            }
            if (game.GameStatus == GameStatus.ended || game.GameStatus == GameStatus.lobby)
            {
                return new SRoundsResponse
                {
                    status = 428,
                    msg = "This action is not allowed at this time",
                    data = null
                };
            }

            // Verificar contraseña
            if (!string.IsNullOrEmpty(game.GamePassword))
            {
                if (string.IsNullOrEmpty(password) || game.GamePassword != password)
                {
                    return new SRoundsResponse
                    {
                        status = 401,
                        msg = "Invalid credentials",
                        data = null
                    };
                }
            }

            // Verificar jugador
            var currentPlayer = game.Players.FirstOrDefault(p => p.PlayerName == player);
            if (currentPlayer == null)
            {
                return new SRoundsResponse
                {
                    status = 403,
                    msg = "Player is not part of the game",
                    data = null
                };
            }

            // Verificar ronda
            var round = await _roundsCollection.Find(r => r.gameId == gameId && r.id == roundId).SingleOrDefaultAsync();
            if (round == null)
            {
                return new SRoundsResponse
                {
                    status = 404,
                    msg = "The specified round was not found",
                    data = null
                };
            }

            // Validaciones de estado y reglas
            if (round.status != "waiting-on-group")
            {
                return new SRoundsResponse
                {
                    status = 428,
                    msg = "This action is not allowed at this time",
                    data = null
                };
            }


            int requiredGroupSize = GetRequiredGroupSize(game.Players.Count, round.roundCount);
            if (round.group.Count != requiredGroupSize)
            {
                return new SRoundsResponse
                {
                    status = 428,
                    msg = $"The group size must be {requiredGroupSize}",
                    data = null
                };
            }

            if (!round.group.Contains(player))
            {
                return new SRoundsResponse
                {
                    status = 403,
                    msg = "You cannot contribute in this round",
                    data = null
                };
            }

            if (currentPlayer.PlayerAction != "none")
            {
                return new SRoundsResponse
                {
                    status = 409,
                    msg = "You have already contributed",
                    data = null
                };
            }

            if (!action && currentPlayer.PlayerRole != "enemy")
            {
                return new SRoundsResponse
                {
                    status = 403,
                    msg = "You cannot contribute in this round",
                    data = null
                };
            }

            // Registrar la acción del jugador
            var playerIndex = game.Players.FindIndex(p => p.PlayerName == player);
            game.Players[playerIndex].PlayerAction = action ? "collaborate" : "sabotage";
            round.actions.Add(action);

            // Actualizar timestamps
            round.updatedAt = DateTime.UtcNow;
            game.UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

            // Procesar completitud de la ronda
            if (IsGroupActionsComplete(round))
            {
                // Determinar resultado
                round.status = "ended";
                round.result = round.actions.Contains(false) ? "enemies" : "citizens";
                await _roundsCollection.ReplaceOneAsync(r => r.id == roundId && r.gameId == gameId, round);

                // Verificar fin del juego
                if (await CheckGameEndConditionAsync(game, round.result))
                {
                    await EndGameAsync(game, round.result, round);
                }
                else
                {
                    // Solo crear nueva ronda si no hay ganador
                    var newRound = await CreateNewRoundAsync(game, round.roundCount);
                    game.CurrentRound = newRound.id;
                    game.UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                    await _gamesCollection.ReplaceOneAsync(g => g.GameId == gameId, game);
                }
            }
            else
            {
                // Si la ronda no está completa, actualizamos ambos documentos
                await _roundsCollection.ReplaceOneAsync(r => r.id == roundId && r.gameId == gameId, round);
                await _gamesCollection.ReplaceOneAsync(g => g.GameId == gameId, game);
            }

            return await GetRoundDetailAsync(gameId, roundId, player, password);
        }

        private async Task<bool> CheckGameEndConditionAsync(Game game, string winningTeam)
        {
            // Filtrar solo las rondas completadas hasta la ronda actual
            var filter = Builders<Round>.Filter.And(
                Builders<Round>.Filter.Eq(r => r.gameId, game.GameId),
                Builders<Round>.Filter.Eq(r => r.result, winningTeam),
                Builders<Round>.Filter.Eq(r => r.status, "ended")
            );

            long victories = await _roundsCollection.CountDocumentsAsync(filter);

            if (victories >= 3)
            {
                // Si encontramos que hay un ganador, asegurémonos de que el juego se marque como terminado
                if (game.GameStatus != GameStatus.ended)
                {
                    game.GameStatus = GameStatus.ended;
                    await _gamesCollection.ReplaceOneAsync(g => g.GameId == game.GameId, game);
                }
                return true;
            }

            return false;
        }

        // Método para terminar el juego
        private async Task EndGameAsync(Game game, string winningTeam, Round round)
        {
            game.GameStatus = GameStatus.ended;
            round.result = winningTeam;
            round.status = "ended";
            round.updatedAt = DateTime.UtcNow;
            game.UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            await _gamesCollection.ReplaceOneAsync(g => g.GameId == game.GameId, game);
            await _roundsCollection.ReplaceOneAsync(r => r.id == round.id && r.gameId == game.GameId, round);
        }

        //Función para verificar si todos los jugadores del grupo han votado
        private bool IsGroupActionsComplete(Round round) { return round.actions.Count == round.group.Count; }
    }
}

