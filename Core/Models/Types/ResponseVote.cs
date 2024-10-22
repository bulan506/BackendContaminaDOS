
namespace Core.Models.Types
{
    public class ResponseVote
    {
        public int status { get; set; }
        public string msg { get; set; }
        public DataVote dataVote { get; set; }
    }
}
