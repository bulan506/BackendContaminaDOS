namespace Core.Models.Data
{

    public class MongoDbSettings
    {
        public string ConnectionString { get; set; }
        public string DatabaseName { get; set; }
        public string GamesCollectionName { get; set; }
        public string RoundsCollectionName { get; set; }
    }
}