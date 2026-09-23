using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RestApi.Data;
using RestApi.Models;
using RestApi.Models.Dto;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;

namespace RestApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly RestApi.Services.PasswordManager _passwordManager;

        public AuthController(ApplicationDbContext context, IConfiguration configuration, RestApi.Services.PasswordManager passwordManager)
        {
            _configuration = configuration;
            _context = context;
            _passwordManager = passwordManager;
        }


        [HttpPost("register")]
        public IActionResult Register(User user)
        {
            if (ModelState.IsValid == false)
            {
                return BadRequest(ModelState);
            }
            user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);
            _context.Users.Add(user);
            _context.SaveChanges();
            return Ok(user);
        }

        [HttpPost("login")]
        public IActionResult Login(UserLoginDto userDto)
        {
            if (ModelState.IsValid == false)
            {
                return BadRequest(ModelState);
            }
            var user = new User
            {
                Username = userDto.Username,
                Password = userDto.Password
            };
            // Encrypt the password
            string passwordHash1 = BCrypt.Net.BCrypt.HashPassword(user.Password);
            Console.WriteLine("Hashed Password: " + passwordHash1);

            // veriyi şifrele ve çöz
            string newPass = _passwordManager.Encrypt(user.Password);
            Console.WriteLine("Encrypted Password: " + newPass);

            string decryptedPass = _passwordManager.Decrypt(newPass);
            Console.WriteLine("Decrypted Password: " + decryptedPass);

            var existingUser = _context.Users.FirstOrDefault(u => u.Username == user.Username);
            if (existingUser != null)
            {
                // Check if the password is correct
                if (BCrypt.Net.BCrypt.Verify(user.Password, existingUser.Password))
                {
                    Console.WriteLine("Password is correct");
                }
                else
                {
                    return Unauthorized("Email or password is incorrect");
                }
            }
            else
            {
                return Unauthorized("Email or password is incorrect");
            }

            // Generate JWT token
            var tokenHandler = new JwtSecurityTokenHandler();
            var JwtKey = _configuration.GetValue<string>("Jwt:Key") ?? "";
            double ExpiresTime = 1;
            var key = Encoding.ASCII.GetBytes(JwtKey);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.Name, existingUser.Username)
                }),
                Expires = DateTime.UtcNow.AddHours(ExpiresTime),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            if (tokenDescriptor != null)
            {
                ParseRole(existingUser.Role, tokenDescriptor);
            }

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);
            Response.Cookies.Append("X-Access-Token", tokenString, new CookieOptions() { HttpOnly = true, SameSite = SameSiteMode.Strict, Expires = DateTime.Now.AddHours(ExpiresTime) });

            return Ok(new { Token = tokenString });
        }

        private void ParseRole(string roles, SecurityTokenDescriptor tokenDescriptor)
        {
            var roleList = roles.Split(',').Select(r => r.Trim()).ToList();
            foreach (var role in roleList)
            {
                tokenDescriptor.Subject.AddClaim(new Claim(ClaimTypes.Role, role));
            }
        }

        [HttpPost("LoginSql")]
        public IActionResult LoginSql(UserLoginDto userDto)
        {
            if (ModelState.IsValid == false)
            {
                return BadRequest(ModelState);
            }
            // Password = "' or 1 = 1 --";
            // hasan@mail.com'--
            //var query = "select * from users where Username = '" + userDto.Username + "' and Password = '" + userDto.Password + "'";
            //Console.WriteLine(query);
            //var raw = _context.Users.FromSqlRaw(query).FirstOrDefault();

            var query = "select * from users where Username = {0} and Password = {1}";
            var raw = _context.Users.FromSqlRaw(query, userDto.Username, userDto.Password).FirstOrDefault();
            return Ok(new { raw });
        }

    } 


}

// select * from users where Username = ''; Drop table users; --a@a.com' and Password = '23423423'

// var UserName = "ali@mail.com" 
// var Passsword = "12345"
// select * from users where email = '"+UserName+"' and password = '"+Passsword+"'


// var UserName = "a@a.com" 
// var Passsword = "' or 1 = 1 --"
// select * from users where email = 'a@a.com' and password = '' or 1 = 1 --'