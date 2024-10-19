namespace Core.Models.Types
{
    public class ResponseCreate
    {
        public int status { get; set; }
        public string msg { get; set; }
        public DataCreate data { get; set; }
    }
}