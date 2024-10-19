namespace Core.Models.Types
{
    public class SRoundsResponse
    {
        public int status { get; set; }
        public string msg { get; set; }
        public DataRounds data { get; set; }
        public List<ErrorDetail> others { get; set; }
    }
}