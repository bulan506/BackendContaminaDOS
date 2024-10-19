namespace Core.Models.Types
{

    public class GameResponse
    {
        public int status { get; set; }
        public string msg { get; set; }
        public List<GameSearch> data { get; set; }
        public Dictionary<string, string> others { get; set; }
    }
}