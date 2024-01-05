using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using UserSystemAPI.Configurations;
using UserSystemAPI.Models;
using UserSystemAPI.Models.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Cors;

namespace UserSystemAPI.Controllers;
[ApiController]
[Route("[controller]")]
public class AuthManagementController : ControllerBase
{
    private readonly ILogger<AuthManagementController> _logger;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly JwtConfig _jwtConfig;
    private readonly RoleManager<IdentityRole> _roleManager;
    
    public AuthManagementController(
        ILogger<AuthManagementController> logger,
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptionsMonitor<JwtConfig> optionsMonitor)
    {
        _logger = logger;
        _userManager = userManager;
        _roleManager = roleManager;
        _jwtConfig = optionsMonitor.CurrentValue;
    }

    private async Task<List<Claim>> GetAllValidClaims(IdentityUser user)
    {
                   var claims = new List<Claim>
                    {
                        new Claim("Id", user.Id),
                        new Claim(ClaimTypes.NameIdentifier, user.Id),
                        new Claim(JwtRegisteredClaimNames.Email, user.Email),
                        new Claim(JwtRegisteredClaimNames.Sub, user.Email),
                        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                    };
        
                    // Getting the claims that we have assigned to the user
                    var userClaims = await _userManager.GetClaimsAsync(user);
                    claims.AddRange(userClaims);
        
                    // Get the user role and add it to the claims
                    var userRoles = await _userManager.GetRolesAsync(user);
        
                    foreach (var userRole in userRoles)
                    {
                        var role = await _roleManager.FindByNameAsync(userRole);
        
                        if (role != null)
                        {
                            claims.Add(new Claim(ClaimTypes.Role, userRole));
        
                            var roleClaims = await _roleManager.GetClaimsAsync(role);
                            foreach (var roleClaim in roleClaims)
                            {
                                claims.Add(roleClaim);
                            }
                        }
                    }
        
                    return claims;
    }
    

    [HttpPost]
    [Route("Register")]
    public async Task<IActionResult> Register([FromBody] UserRegistrationRequestDto requestDto)
    {
        if (ModelState.IsValid)
        {
            var emailExist = await _userManager.FindByEmailAsync(requestDto.Email);

            if (emailExist != null)
                return BadRequest("Email already exists");

            var newUser = new IdentityUser()
            {
                Email = requestDto.Email,
                UserName = requestDto.Email,
            };

            var isCreated = await _userManager.CreateAsync(newUser, requestDto.Password);

            if (isCreated.Succeeded)
            {
                await _userManager.AddToRoleAsync(newUser, "AppUser");
                var token = GenerateJwtToken(newUser);
                
                return Ok(token);
            }

            return BadRequest( isCreated.Errors.Select(x => x.Description).ToList());

        }
        
        return BadRequest("Invalid request payload");
    }

    [HttpPost]
    [Route("Login")]
    public async Task<IActionResult> Login(UserLoginRequestDto requestDto)
    {
        if (ModelState.IsValid)
        {
            var existingUser = await _userManager.FindByEmailAsync(requestDto.Email);

            if (existingUser == null)
                return BadRequest("Invalid Auth");

            var isPasswordValid = await _userManager.CheckPasswordAsync(existingUser, requestDto.Password);

            if (isPasswordValid)
            {
                var token = GenerateJwtToken(existingUser);

                return Ok(await token);
            }
        }
        return BadRequest("invalid auth");
    }
    
    private async Task<AuthResult> GenerateJwtToken(IdentityUser user)
    {
        var jwtTokenHandler = new JwtSecurityTokenHandler();

        var key = Encoding.ASCII.GetBytes(_jwtConfig.Secret);
        var claims = await GetAllValidClaims(user);
        
        var tokenDescriptor = new SecurityTokenDescriptor()
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(4),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha512)
        };

        var token = jwtTokenHandler.CreateToken(tokenDescriptor);
        var jwtToken = jwtTokenHandler.WriteToken(token);

        return new AuthResult()
        {
            Token = jwtToken,
            Result = true
        };
    }
    
}