namespace Core.Models.Types
{
    public class RoundsResponse
    {

        public int status { get; set; }
        public string msg { get; set; }
        public List<DataRounds> data { get; set; }
        public List<ErrorDetail> others { get; set; }

    }

}