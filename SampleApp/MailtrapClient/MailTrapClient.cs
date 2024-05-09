namespace MailtrapClient;

using MailKit.Net.Smtp;
using MimeKit;
using Polly;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NLog;
using System.Net.Http.Headers;

public class MailTrapClient : IMailtrapEmailSender
{
    private readonly Logger logger = LogManager.GetCurrentClassLogger();
    private SmtpClient _smtpClient;

    protected SmtpClient MySmtpClient
    {
        get { return _smtpClient; }
    }
    
    private readonly MailtrapConfig _config;

    public MailTrapClient(MailtrapConfig config)
    {
        _config = config;
        _smtpClient = new SmtpClient();
        if (String.IsNullOrWhiteSpace(_config.apiKey) && String.IsNullOrWhiteSpace(_config.baseUrl))
        {
            _smtpClient.Connect(_config.Host, _config.Port, _config.UseSsl);
            _smtpClient.Authenticate(_config.Username, _config.Password);
        }
        
    }

    public MailTrapClient(string _Host, int _Port, bool _UseSsl, string _Username, string _Password )
    {
        _config = new MailtrapConfig {
            Host = _Host,
            Port = _Port,
            UseSsl = _UseSsl,
            Username = _Username,
            Password = _Password,
        };
        _smtpClient = new SmtpClient();
        _smtpClient.Connect(_config.Host, _config.Port, _config.UseSsl);
        _smtpClient.Authenticate(_config.Username, _config.Password);
        
    }

    public MailTrapClient(string _apiKey, string _baseUrl )
    {
        _config = new MailtrapConfig {
            apiKey = _apiKey,
            baseUrl = _baseUrl
        };
        _smtpClient = null;
        
    }

    public async Task<bool> SendAsync(MailtrapEmail email)
    {
        var mimeMessage = CreateMimeMessage(email);

        var retryPolicy = Policy
            .Handle<Exception>() // Customize exception types as needed
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));


        try
        {
            if (!String.IsNullOrWhiteSpace(_config.apiKey) && !String.IsNullOrWhiteSpace(_config.baseUrl))
            {
                var client = new HttpClient();
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"{_config.baseUrl}/api/send"),
                    Headers =
                    {
                        { "Accept", "application/json" },
                        { "Api-Token", _config.apiKey},
                    },
                    Content = new StringContent(mimeMessage.ToString())
                    {
                        Headers =
                        {
                            ContentType = new MediaTypeHeaderValue("application/json")
                        }
                    }
                };
                await retryPolicy.ExecuteAsync(async () =>
                {
                    using (var response = await client.SendAsync(request))
                    {
                        response.EnsureSuccessStatusCode();
                    }
                });
            }else
            {
                if (_smtpClient != null)
                {
                    await retryPolicy.ExecuteAsync(async () =>
                    {
                        if (!_smtpClient.IsConnected)
                        {
                            await _smtpClient.ConnectAsync(_config.Host, _config.Port, _config.UseSsl);
                        }

                        if (!_smtpClient.IsAuthenticated)
                        {
                            await _smtpClient.AuthenticateAsync(_config.Username, _config.Password);
                        }
                        
                        await _smtpClient.SendAsync(mimeMessage);
                        await _smtpClient.DisconnectAsync(true);
                    });
                }
                
            }
            return true;
        }
        catch (Exception ex)
        {
            // Handle exceptions here: log the error, throw a custom exception, etc.
            // Example: Log the error and throw a custom exception
            Console.WriteLine($"Error sending email: {ex.Message}");
            logger.Error(ex, "Error sending email.");
            throw new MailtrapException($"Error sending email: {ex.Message}", ex);
        }
        finally
        {
            _smtpClient.Disconnect(true);
        }
    }

    private MimeMessage CreateMimeMessage(MailtrapEmail message)
    {

        var builder = new BodyBuilder();
        var mimeMessage = new MimeMessage();
            mimeMessage.From.Add(new MailboxAddress(message.SenderName, message.SenderEmail));
            mimeMessage.To.Add(new MailboxAddress(message.RecipientName, message.RecipientEmail));
            mimeMessage.Subject = message.Subject;
            mimeMessage.Body = new TextPart("plain") { Text = !string.IsNullOrEmpty(message.Text) ? message.Text : "" };
            if (!string.IsNullOrEmpty(message.Html))
            {
                mimeMessage.Body = new TextPart("html") { Text = message.Html };
            }
            

            if (!string.IsNullOrEmpty(message.Text))
            {
                builder.TextBody = message.Text;
            }
            if (!string.IsNullOrEmpty(message.Html))
            {
                builder.HtmlBody = message.Html;
            }
        // Add attachments if needed
        if (message.Attachments != null && message.Attachments.Count > 0)
            {
                foreach (var attachment in message.Attachments)
                {
                    message.Attachments.Add(attachment);
                }
            }
        mimeMessage.Body = builder.ToMessageBody();
        return mimeMessage;
    }

    public void SetSmtpClient(SmtpClient smtpClient)
    {
        this._smtpClient = smtpClient;
    }
}

public interface IMailtrapEmailSender
{
    Task<bool> SendAsync(MailtrapEmail message);
}

public class MailtrapConfig
{
    public string Host { get; set; }
    public int Port { get; set; }
    public bool UseSsl { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string apiKey { get; set; }
    public string baseUrl { get; set; }
}

public class MailtrapEmail
{
    public string SenderName { get; set; }
    public required string SenderEmail { get; set; }
    public string RecipientName { get; set; }
    public required string RecipientEmail { get; set; }
    public required string Subject { get; set; }
    public string Text { get; set; }
    public string Html { get; set; }
    public List<MimePart> Attachments { get; set; }
}

public class MailtrapException : Exception
{
    public MailtrapException(string message, Exception innerException) : base(message, innerException)
    {
    }
}