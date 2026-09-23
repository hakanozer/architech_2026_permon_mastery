using System.ComponentModel.DataAnnotations;

namespace RestApi.CustomValids
{
    public class CityValid : ValidationAttribute
    {
        protected override ValidationResult IsValid(object? value, ValidationContext validationContext)
        {
            string[] validCities = { "istanbul", "ankara", "izmir" };
            if (validCities.Contains(value?.ToString()!.ToLower()))
            {
                return ValidationResult.Success;
            }
            else
            {
                return new ValidationResult("City must be one of the following: istanbul, ankara, izmir");
            }
        }
    }
   
}