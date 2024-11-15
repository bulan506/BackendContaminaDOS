namespace Core.Models.Types
{
    public class DataRounds
    {
        public string id { get; set; }
        public string leader { get; set; }
        public string status { get; set; }
        public string result { get; set; }
        public string phase { get; set; }
        public List<string> group { get; set; }
        public List<bool> votes { get; set; }
        public DateTime createdAt { get; set; }
        public DateTime updatedAt { get; set; }
    }
}
