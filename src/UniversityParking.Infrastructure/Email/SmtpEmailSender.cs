using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MimeKit;
using UniversityParking.Application.Common.Abstractions;

namespace UniversityParking.Infrastructure.Email;
public sealed class EmailDeliveryOptions { public bool Enabled { get; set; } public string Provider { get; set; } = "Smtp"; }
public sealed class SmtpOptions
{
    public string Host {get;set;}=""; public int Port {get;set;}=587; public bool UseStartTls {get;set;}=true;
    public string Username {get;set;}=""; public string Password {get;set;}="";
    public string FromEmail {get;set;}=""; public string FromName {get;set;}="MotorBike Park";
}
public sealed class EmailDeliveryException : Exception { public EmailDeliveryException() : base("El envío de correo no está disponible.") {} }
public sealed class SmtpEmailSender(IOptions<EmailDeliveryOptions> delivery,IOptions<SmtpOptions> smtp) : IEmailSender,IDisposable
{
    public bool IsEnabled=>delivery.Value.Enabled;
    private MailKit.Net.Smtp.SmtpClient? client;
    public async Task EnsureAvailableAsync(CancellationToken token)
    {
        if(!IsEnabled)throw new EmailDeliveryException();
        if(client is {IsConnected:true,IsAuthenticated:true})return;
        client?.Dispose();client=new MailKit.Net.Smtp.SmtpClient{Timeout=15000};
        try
        {
            var options=smtp.Value;
            await client.ConnectAsync(options.Host,options.Port,options.UseStartTls?SecureSocketOptions.StartTls:SecureSocketOptions.SslOnConnect,token);
            await client.AuthenticateAsync(options.Username,options.Password,token);
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested){throw;}
        catch{throw new EmailDeliveryException();}
    }
    public void Dispose()=>client?.Dispose();
    public async Task SendAsync(string to,string subject,string body,CancellationToken token)
    {
        if(!delivery.Value.Enabled) throw new EmailDeliveryException();
        try
        {
            var options=smtp.Value;
            var message=new MimeMessage(); message.From.Add(new MailboxAddress(options.FromName,options.FromEmail));
            message.To.Add(MailboxAddress.Parse(to)); message.Subject=subject; message.Body=new TextPart("plain"){Text=body};
            await EnsureAvailableAsync(token);
            await client!.SendAsync(message,token);
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested) { throw; }
        catch { throw new EmailDeliveryException(); }
    }
}
public static class EmailRegistration
{
    public static void AddEmailDelivery(IServiceCollection services,IConfiguration config)
    {
        var enabled=config.GetValue<bool>("EmailDelivery:Enabled");
        services.AddOptions<EmailDeliveryOptions>().Bind(config.GetSection("EmailDelivery"))
            .Validate(x=>x.Provider=="Smtp","Proveedor de correo inválido.").ValidateOnStart();
        services.AddOptions<SmtpOptions>().Bind(config.GetSection("Smtp"))
            .Validate(x=>!enabled || (!string.IsNullOrWhiteSpace(x.Host) && x.Port is >0 and <=65535 && !string.IsNullOrWhiteSpace(x.Username) &&
                !string.IsNullOrWhiteSpace(x.Password) && ValidSender(x.FromEmail)),"Configuración SMTP incompleta.").ValidateOnStart();
        services.AddScoped<IEmailSender,SmtpEmailSender>();
    }
    private static bool ValidSender(string value) => !string.IsNullOrWhiteSpace(value) &&
        System.Net.Mail.MailAddress.TryCreate(value, out var address) && address.Address == value.Trim() &&
        address.Host.Contains('.') && !value.Any(char.IsWhiteSpace);
}
