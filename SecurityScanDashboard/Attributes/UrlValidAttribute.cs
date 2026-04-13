using System.ComponentModel.DataAnnotations;

namespace SecurityScanDashboard.Attributes
{
    public class UrlValidAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return ValidationResult.Success; // Required attribute will handle this
            }

            string url = value.ToString()!;

            // Try to parse as URI
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uriResult))
            {
                return new ValidationResult(
                    ErrorMessage ?? "Geçerli bir URL giriniz (örnek: https://example.com)"
                );
            }

            // Must be http or https
            if (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps)
            {
                return new ValidationResult(
                    ErrorMessage ?? "URL http:// veya https:// ile başlamalıdır"
                );
            }

            return ValidationResult.Success;
        }
    }
}
