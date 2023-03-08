Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .WriteTo.File("./Logs/log.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();
try
{
    await OpcuaManagement.CreateServerInstance();
    Console.WriteLine("OPC UA 服務已啟動...");
    Console.ReadLine();
}
catch (Exception e)
{
    Log.Fatal("[{0}] {1}", nameof(Program), new
    {
        e.Message,
        e.StackTrace
    });
}
finally
{
    Log.CloseAndFlush();
}