namespace Core.Models.Types
{
    public class ErrorResponse
    {
        public int status { get; set; }
        public string msg { get; set; }
        public object data { get; set; }
        public List<ErrorDetail> others { get; set; }
    }

    public class ErrorDetail
    {
        public int status { get; set; }
        public string msg { get; set; }
    }
}