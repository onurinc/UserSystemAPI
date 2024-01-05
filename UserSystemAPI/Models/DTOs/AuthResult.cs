namespace UserSystemAPI.Models.DTOs;

public class AuthResult
{
    public string Token { get; set; } = String.Empty;
    public bool Result { get; set; }
    
    public List<string> Errors { get; set; }

}