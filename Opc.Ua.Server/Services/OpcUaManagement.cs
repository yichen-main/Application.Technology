namespace Axis.OpcUa.Station.Services;
public class OpcuaManagement
{
    public static void CreateServerInstance()
    {
        try
        {
            ApplicationConfiguration configuration = new()
            {
                ApplicationName = "FEELER-SMB",
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
                        StoreType = @"Directory",
                        StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\MachineDefault",
                        SubjectName = Utils.Format(@$"CN={"OpcUa"}, DC={Dns.GetHostName()}")
                    },
                    TrustedIssuerCertificates = new()
                    {
                        StoreType = @"Directory",
                        StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Certificate Authorities"
                    },
                    TrustedPeerCertificates = new()
                    {
                        StoreType = @"Directory",
                        StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Applications"
                    },
                    RejectedCertificateStore = new()
                    {
                        StoreType = @"Directory",
                        StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\RejectedCertificates"
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

            configuration.Validate(ApplicationType.Server).GetAwaiter().GetResult();
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

            var certOk = application.CheckApplicationInstanceCertificate(false, 0).Result;

            if (!certOk) Console.WriteLine("證書驗證失敗!");

            var dis = new DiscoveryServerBase();

            application.Start(new OpcuaServer()).Wait();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("啟動 OPC UA 服務端觸發異常:" + ex.Message);
            Console.ResetColor();
        }
    }
}
