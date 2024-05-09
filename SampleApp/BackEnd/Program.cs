using MailtrapClient;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.MapGet("/sendEmail", async () =>
{
     var config = new MailtrapConfig
        {
            Username = "d6617ce2801b98",
            Password = "2644d9c3897df4",
            Host = "sandbox.smtp.mailtrap.io",
            Port = 2525, //25 or 465 or 587 or 2525
            UseSsl = false,
        };
    var _mailtrapClient = new MailTrapClient(config);

    var message = new MailtrapEmail
        {
            SenderName = "Test Sender",
            SenderEmail = "sender@example.com",
            RecipientName = "Test Recipient",
            RecipientEmail = "recipient@example.com",
            Subject = "Test Subject From Api",
            Text = "Test Body From Api"
        };

    // Act
    var result = await _mailtrapClient.SendAsync(message);

    return result;
})
.WithName("SendEmail")
.WithOpenApi();


app.Run();

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
