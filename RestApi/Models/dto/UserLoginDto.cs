using System.ComponentModel.DataAnnotations;
using RestApi.CustomValids;

namespace RestApi.Models.Dto

{
    public class UserLoginDto
    {

        [Required]
        [StringLength(maximumLength: 150, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 150 characters")]
        [EmailAddress(ErrorMessage = "Username must be an email address")]
        public string Username { get; set; } = string.Empty;

        [Required]
        //[StringLength(maximumLength: 10, MinimumLength = 5, ErrorMessage = "Password must be between 5 and 10 characters")]
        //[RegularExpression(@"^[a-zA-Z0-9]+$", ErrorMessage = "Password must be alphanumeric")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [CityValid(ErrorMessage = "City must be one of the following: istanbul, ankara, izmir")]
        public string City { get; set; } = string.Empty;
    }
}