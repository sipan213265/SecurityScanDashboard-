using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace SecurityScanDashboard.Attributes
{
    public class GitHubUrlAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return ValidationResult.Success; // Required attribute will handle this
            }

            string url = value.ToString()!;

            // GitHub URL pattern: https://github.com/owner/repo or http://github.com/owner/repo
            var githubPattern = @"^https?://(www\.)?github\.com/[\w\-\.]+/[\w\-\.]+/?.*$";
            
            if (!Regex.IsMatch(url, githubPattern, RegexOptions.IgnoreCase))
            {
                return new ValidationResult(
                    ErrorMessage ?? "Geçerli bir GitHub repository URL'si giriniz (örnek: https://github.com/owner/repo)"
                );
            }

            return ValidationResult.Success;
        }
    }
}
