namespace Core.Models.Types
{
    public class ResponseAllGames
    {
        public int status { get; set; }
        public string msg { get; set; }
        public List<DataCreate> data { get; set; }
        public List<ErrorDetail> others { get; set; }
    }
}