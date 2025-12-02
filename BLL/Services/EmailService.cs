using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services
{
    public interface IEmailService
    {
        Task<bool> SendResetPasswordEmailAsync(string toEmail, string resetToken, string userName);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<bool> SendResetPasswordEmailAsync(string toEmail, string resetToken, string userName)
        {
            try
            {
                var smtpHost = _configuration["Email:SmtpHost"];
                var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
                var smtpUser = _configuration["Email:SmtpUser"];
                var smtpPassword = _configuration["Email:SmtpPassword"];
                var fromEmail = _configuration["Email:FromEmail"];
                var fromName = _configuration["Email:FromName"] ?? "TaskHub";

                using var smtpClient = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUser, smtpPassword),
                    EnableSsl = true
                };

                var resetLink = $"{_configuration["App:FrontendUrl"]}/reset-password?token={resetToken}";

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = "Đặt lại mật khẩu - TaskHub",
                    Body = GenerateResetPasswordEmailTemplate(userName, resetLink),
                    IsBodyHtml = true,
                    BodyEncoding = Encoding.UTF8, // Thêm dòng này
                    SubjectEncoding = Encoding.UTF8 // Thêm dòng này
                };

                mailMessage.To.Add(toEmail);

                await smtpClient.SendMailAsync(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Send email error: {ex.Message}");
                return false;
            }
        }

        private string GenerateResetPasswordEmailTemplate(string userName, string resetLink)
        {
            return $@"
        <html>
        <head>
            <meta charset='UTF-8'>
            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            <style>
                .container {{ max-width: 600px; margin: 0 auto; font-family: Arial, sans-serif; }}
                .header {{ background-color: #2563eb; color: white; padding: 20px; text-align: center; }}
                .content {{ padding: 20px; background-color: #f9fafb; }}
                .button {{ background-color: #2563eb; color: white; padding: 12px 24px; text-decoration: none; border-radius: 6px; display: inline-block; margin: 20px 0; }}
                .footer {{ padding: 20px; text-align: center; color: #6b7280; font-size: 14px; }}
            </style>
        </head>
        <body>
            <div class='container'>
                <div class='header'>
                    <h1>TaskHub</h1>
                </div>
                <div class='content'>
                    <h2>Xin chào {userName},</h2>
                    <p>Bạn đã yêu cầu đặt lại mật khẩu cho tài khoản TaskHub của mình.</p>
                    <p>Nhấp vào nút bên dưới để đặt lại mật khẩu:</p>
                    <a href='{resetLink}' class='button'>Đặt lại mật khẩu</a>
                    <p>Liên kết này sẽ hết hạn sau 1 giờ.</p>
                    <p>Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.</p>
                </div>
                <div class='footer'>
                    <p>© 2024 TaskHub. Tất cả quyền được bảo lưu.</p>
                </div>
            </div>
        </body>
        </html>";
        }
    }
}