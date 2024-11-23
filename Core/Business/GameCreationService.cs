using System;
using System.Linq;
using System.Collections.Generic;
using Core.Models.Data;
using Core.Models.Types;
using MongoDB.Driver;
using System.Text.RegularExpressions;

namespace Core.Models.Business
{
    public class GameCreationService : IGameCreationService
    {
        private readonly IMongoCollection<Game> _gamesCollection;

        public GameCreationService(DbSettings settings)
        {
            var client = new MongoClient(settings.ConnectionString);
            var database = client.GetDatabase(settings.DatabaseName);
            _gamesCollection = database.GetCollection<Game>(settings.GamesCollectionName);
        }

        public async Task<ResponseCreate> CreateGameAsync(RequestGame requestGame)
        {
            var gameFound = await _gamesCollection.Find(g => g.GameName == requestGame.name).AnyAsync();
            if (gameFound)
            {
                return new ResponseCreate
                {
                    status = 409,
                    msg = "Asset already exists",
                    data = null
                };
            }
            //  DeleteAllGames();
            var game = new Game
            {
                GameId = Guid.NewGuid().ToString(),
                GameName = requestGame.name,
                GameOwner = requestGame.owner,
                CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                CurrentRound = "000000000000000000000000000000000000",
                GameStatus = GameStatus.lobby,
                GamePassword = requestGame.password,
                Players = new List<Player>
                {
                    new Player
                    {
                        PlayerId = Guid.NewGuid().ToString(),
                        PlayerName = requestGame.owner,
                        PlayerType = "owner",
                        PlayerRole = "citizen",
                        PlayerVote = "none",
                        PlayerAction= "none"
                    }
                },
                Enemies = []
            };

            await _gamesCollection.InsertOneAsync(game);

            var data = new DataCreate
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
                enemies = game.Players.Where(p => p.PlayerRole == "enemy").Select(p => p.PlayerName).ToList()
            };

            return new ResponseCreate
            {
                status = 201,
                msg = "Game Created",
                data = data
            };
        }
        public async Task<ResponseCreate> GetGameAsync(string id, string player, string password = null)
        {
            var game = await _gamesCollection.Find(g => g.GameId == id).FirstOrDefaultAsync();

            if (game == null)
            {
                return new ResponseCreate
                {
                    status = 404,
                    msg = "Game does not exists",
                    data = { }
                };
            }


            // Verify if a password is required
            if (!string.IsNullOrEmpty(game.GamePassword))
            {
                // If no password is provided
                if (string.IsNullOrEmpty(password))
                {
                    return new ResponseCreate
                    {
                        status = 401,
                        msg = "Invalid credentials",
                        data = null
                    };
                }
            }
            var currentPlayer = game.Players.FirstOrDefault(p => p.PlayerName == player);

            var data = new DataCreate
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
                enemies = currentPlayer != null && currentPlayer.PlayerRole == "enemy"
            ? game.Players.Where(p => p.PlayerRole == "enemy").Select(p => p.PlayerName).ToList()
            : new List<string>()
            };

            if (currentPlayer == null)
            {
                return new ResponseCreate
                {
                    status = 403,
                    msg = "Invalid credentials",
                    data = data
                };
            }
            if (game.GamePassword != password)
            {
                return new ResponseCreate
                {
                    status = 401,
                    msg = "Invalid credentials",
                    data = data
                };
            }

            return new ResponseCreate
            {
                status = 200,
                msg = "Game found",
                data = data
            };
        }
        public async Task<ResponseAllGames> SearchGamesAsync(string name = null, string status = null, int page = 0, int limit = 50)
        {
            var errorResponse = new ErrorResponse
            {
                status = 400,
                msg = "Invalid request parameters",
                data = null,
                others = new List<ErrorDetail>()
            };

            bool hasError = false;

            // Validar la página
            if (page < 0)
            {
                hasError = true;
                errorResponse.others.Add(new ErrorDetail
                {
                    status = 400,
                    msg = "Invalid page number"
                });
            }

            // Validar el límite
            if (limit <= 0 || limit > 250)
            {
                hasError = true;
                errorResponse.others.Add(new ErrorDetail
                {
                    status = 400,
                    msg = "Invalid limit number"
                });
            }

            if (hasError)
            {
                errorResponse.msg = errorResponse.others[0].msg;
                return new ResponseAllGames
                {
                    status = 400,
                    msg = errorResponse.msg,
                    data = null,
                    others = errorResponse.others
                };
            }

            var filter = Builders<Game>.Filter.Empty;

            if (!string.IsNullOrEmpty(name))
            {
                if (name.EndsWith("\\"))
                {
                    name = name.TrimEnd('\\') + "\\\\";
                }
                string literalName = @"\Q" + name + @"\E";
                filter = filter & Builders<Game>.Filter.Regex(g => g.GameName, new MongoDB.Bson.BsonRegularExpression(literalName, "i"));
            }

            if (!string.IsNullOrEmpty(status))
            {

                if (Enum.TryParse<GameStatus>(status, true, out var parsedStatus))
                {
                    filter = filter & Builders<Game>.Filter.Eq(g => g.GameStatus, parsedStatus);
                }
                else
                {
                    //  DeleteAllGames()                                                                                          // Si el status no es válido, devolvemos un error
                    return new ResponseAllGames
                    {
                        status = 400,
                        msg = "Invalid game status",
                        data = null,
                        others = new List<ErrorDetail>
                {
                    new ErrorDetail { status = 400, msg = $"Invalid game status" }
                }
                    };
                }
            }

            var games = await _gamesCollection.Find(filter)
                .Skip(page * limit)
                .Limit(limit)
                .ToListAsync();

            var dataListG = games.Select(g => new DataCreate
            {
                id = g.GameId,
                name = g.GameName,
                owner = g.GameOwner,
                status = g.GameStatus.ToString(),
                password = !string.IsNullOrEmpty(g.GamePassword),
                currentRound = g.CurrentRound,
                createdAt = g.CreatedAt,
                updatedAt = g.UpdatedAt,
                players = g.Players.Select(p => p.PlayerName).ToList(),
                enemies = g.Players.Where(p => p.PlayerType == "enemy").Select(p => p.PlayerName).ToList()
            }).ToList();

            return new ResponseAllGames
            {
                status = 200,
                msg = $"Search returned {dataListG.Count} result(s)",
                data = dataListG
            };
        }
        private bool DeleteAllGames()
        {
            try
            {
                var result = _gamesCollection.DeleteMany(Builders<Game>.Filter.Empty);

                // Consideramos la operación exitosa si se eliminó al menos un juego
                return result.DeletedCount > 0;
            }
            catch (Exception)
            {
                // Si ocurre cualquier excepción, consideramos que la operación falló
                return false;
            }
        }
    }
}