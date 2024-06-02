namespace DataProcessorAPI.Models;

public class ResponseMessage(List<ResponseM> response)
{
    public List<ResponseM> Response { get; set; } = response;
}