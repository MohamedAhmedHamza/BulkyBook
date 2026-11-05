using BulkyBook.Business.Services.IServices;
using Mailjet.Client;
using Mailjet.Client.TransactionalEmails;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace BulkyBook.Business.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly string _apiKey;
        private readonly string _secretKey;
        private readonly string _senderEmail;
        private readonly string _senderName;  

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
            _apiKey = _configuration["Mailjet:ApiKey"];
            _secretKey = _configuration["Mailjet:SecretKey"];
            _senderEmail = _configuration["Mailjet:SenderEmail"];
            _senderName = _configuration["Mailjet:SenderName"];
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                MailjetClient client = new MailjetClient(_apiKey, _secretKey);
                var email = new TransactionalEmailBuilder()
                    .WithFrom(new SendContact(_senderEmail, _senderName))
                    .WithSubject(subject)
                    .WithHtmlPart(body)
                    .WithTo(new SendContact(toEmail))
                    .Build();
                var response = await client.SendTransactionalEmailAsync(email);
                if (response.Messages != null && response.Messages.Length > 0)
                {
                    return response.Messages[0].Status == "success";
                }
                return false;
            }
            catch (Exception)
            {
                // Mail problems must never break checkout; callers treat false as 'not sent'.
                return false;
            }
        }

        public async Task<bool> SendOrderConfirmationEmailAsync(string toEmail, int orderId, decimal orderTotal)
        {
            var subject = $"Order Confirmation - Order #{orderId}";
            var body = $"" +
                $"<h1>Thank you for your order!</h1>" +
                $"<p>Your order has been successfully placed. Here are the details:</p>" +
                $"<p><strong>Order Number:</strong> {orderId}</p>" +
                $"<p><strong>Order Date:</strong> {DateTime.Now:yyyy-MM-dd}</p>" +
                $"<p><strong>Total Amount:</strong> {orderTotal:C}</p>" +
                $"<hr/> " +
                $"<p>Thank you for choosing our store!</p>";

            return await SendEmailAsync(toEmail, subject, body);
        }
    }
}
