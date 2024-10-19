using Core.Models.Types;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

public class Game
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }
    public string GameId { get; set; }
    public string GameName { get; set; }
    public string GameOwner { get; set; }
    public string CreatedAt { get; set; }
    public string UpdatedAt { get; set; }
    public string CurrentRound { get; set; }
    public GameStatus GameStatus { get; set; }
    public string GamePassword { get; set; }
    public List<Player> Players { get; set; }
    public List<string> Enemies { get; set; }
}

public class Player
{
    public string PlayerId { get; set; }
    public string PlayerName { get; set; }
    public string PlayerType { get; set; }
    public string PlayerRole { get; set; }
}