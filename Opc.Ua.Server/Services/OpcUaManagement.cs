namespace Axis.OpcUa.Station.Services;
public class OpcuaManagement
{
    public static async Task CreateServerInstance()
    {//https://blog.csdn.net/qq_32663557/article/details/108062820
        try
        {
            var rootPath = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? string.Empty;
            ApplicationConfiguration configuration = new()
            {
                ApplicationName = "FFG.iMDS.IIoT",
                ApplicationUri = Utils.Format(@$"urn:{Dns.GetHostName()}:OpcUa"),
                ApplicationType = ApplicationType.Server,
                ServerConfiguration = new()
                {
                    BaseAddresses = { $"opc.tcp://localhost:6655" },
                    MinRequestThreadCount = 5,
                    MaxRequestThreadCount = 100,
                    MaxQueuedRequestCount = 200
                },
                SecurityConfiguration = new()
                {
                    ApplicationCertificate = new()
                    {
                        StoreType = "Directory",
                        StorePath = Path.Combine(rootPath, "OPC Foundation", "CertificateStores", "MachineDefault"),
                        SubjectName = Utils.Format(@$"CN={"OpcUa"}, DC={Dns.GetHostName()}")
                    },
                    TrustedIssuerCertificates = new()
                    {
                        StoreType = "Directory",
                        StorePath = Path.Combine(rootPath, "OPC Foundation", "CertificateStores", "UA Certificate Authorities")
                    },
                    TrustedPeerCertificates = new()
                    {
                        StoreType = "Directory",
                        StorePath = Path.Combine(rootPath, "OPC Foundation", "CertificateStores", "UA Applications")
                    },
                    RejectedCertificateStore = new()
                    {
                        StoreType = "Directory",
                        StorePath = Path.Combine(rootPath, "OPC Foundation", "CertificateStores", "RejectedCertificates")
                    },
                    AutoAcceptUntrustedCertificates = true,
                    AddAppCertToTrustedStore = true
                },
                TransportConfigurations = new(),
                TransportQuotas = new()
                {
                    OperationTimeout = 15000
                },
                ClientConfiguration = new()
                {
                    DefaultSessionTimeout = 60000
                },
                TraceConfiguration = new()
            };

            await configuration.Validate(ApplicationType.Server);
            if (configuration.SecurityConfiguration.AutoAcceptUntrustedCertificates)
            {
                configuration.CertificateValidator.CertificateValidation += (s, e) =>
                {
                    e.Accept = e.Error.StatusCode == StatusCodes.BadCertificateUntrusted;
                };
            }

            ApplicationInstance application = new()
            {
                ApplicationName = "OpcUa",
                ApplicationType = ApplicationType.Server,
                ApplicationConfiguration = configuration
            };

            var certOk = await application.CheckApplicationInstanceCertificate(default, default);

            if (!certOk) Console.WriteLine("證書驗證失敗!");

            var dis = new DiscoveryServerBase();

            var server = new OpcuaServer();
         

            server.CurrentInstance.SessionManager.SessionClosing += (session, reason) =>
            {//斷開
                var HH = session.SessionDiagnostics.SessionName;

            };
            server.CurrentInstance.SessionManager.ImpersonateUser += (session, args) =>
            {//連上
                var HH = session.SessionDiagnostics.SessionName;

            };
            await application.Start(server);
            var DD = server.CurrentInstance.SessionManager.GetSessions();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("啟動 OPC UA 服務端觸發異常:" + ex.Message);
            Console.ResetColor();
        }
    }
}
