namespace Axis.OpcUa.Station.Services;
public sealed class OpcuaServer : StandardServer
{
    /// <summary>
    /// 為服務器創建節點管理器
    /// </summary>
    /// <remarks>
    /// 此方法允許子類創建它使用的任何其他節點管理器。 開發工具包
    /// 總是創建一個 CoreNodeManager 來處理規範定義的內置節點
    /// 任何額外的 NodeManager 都應該處理特定於應用程序的節點
    /// </remarks>
    protected override MasterNodeManager CreateMasterNodeManager(IServerInternal server, ApplicationConfiguration configuration)
    {
        Utils.Trace("Creating the Node Managers.");
        List<INodeManager> nodeManagers = new()
        {
            //創建自定義節點管理器
            new NodeManager(server, configuration)
        };

        //創建主節點管理器
        return new MasterNodeManager(server, configuration, null, nodeManagers.ToArray());
    }

    /// <summary>
    /// 加載應用程序的不可配置屬性
    /// </summary>
    /// <remarks>
    /// 這些屬性由服務器公開，但管理員無法更改
    /// </remarks>
    protected override ServerProperties LoadServerProperties()
    {
        ServerProperties properties = new()
        {
            ManufacturerName = "OPC Foundation",
            ProductName = "Quickstart Reference Server",
            ProductUri = "http://opcfoundation.org/Quickstart/ReferenceServer/v1.04",
            SoftwareVersion = Utils.GetAssemblySoftwareVersion(),
            BuildNumber = Utils.GetAssemblyBuildNumber(),
            BuildDate = Utils.GetAssemblyTimestamp()
        };
        return properties;
    }

    /// <summary>
    /// 為服務器創建資源管理器
    /// </summary>
    protected override ResourceManager CreateResourceManager(IServerInternal server, ApplicationConfiguration configuration)
    {
        ResourceManager resourceManager = new(server, configuration);
        FieldInfo[] fields = typeof(StatusCodes).GetFields(BindingFlags.Public | BindingFlags.Static);
        foreach (FieldInfo field in fields)
        {
            uint? id = field.GetValue(typeof(StatusCodes)) as uint?;
            if (id is not null)
            {
                resourceManager.Add(id.Value, "en-US", field.Name);
            }
        }
        return resourceManager;
    }

    /// <summary>
    /// 在啟動之前初始化服務器
    /// </summary>
    /// <remarks>
    /// 在任何啟動處理髮生之前調用此方法。 子類可以更新
    /// 配置對像或執行任何其他特定於應用程序的啟動任務
    /// </remarks>
    protected override void OnServerStarting(ApplicationConfiguration configuration)
    {
        Utils.Trace("The server is starting.");
        base.OnServerStarting(configuration);

        //由應用程序決定如何驗證用戶身份令牌
        //此函數為 X509 身份令牌創建驗證器
        CreateUserIdentityValidators(configuration);
    }

    /// <summary>
    /// 服務器啟動後調用
    /// </summary>
    protected override void OnServerStarted(IServerInternal server)
    {
        base.OnServerStarted(server);

        //當用戶身份更改時請求通知。 默認接受所有有效用戶
        server.SessionManager.ImpersonateUser += new ImpersonateEventHandler(SessionManager_ImpersonateUser);

        //try
        //{
        //    // allow a faster sampling interval for CurrentTime node.
        //    server.Status.Variable.CurrentTime.MinimumSamplingInterval = 250;
        //}
        //catch
        //{ }
    }

    #region 用戶驗證功能
    /// <summary>
    /// 創建用於驗證服務器支持的用戶身份令牌的對象
    /// </summary>
    private void CreateUserIdentityValidators(ApplicationConfiguration configuration)
    {
        for (int ii = 0; ii < configuration.ServerConfiguration.UserTokenPolicies.Count; ii++)
        {
            UserTokenPolicy policy = configuration.ServerConfiguration.UserTokenPolicies[ii];

            //為證書令牌策略創建驗證器
            if (policy.TokenType == UserTokenType.Certificate)
            {
                //檢查是否在配置中指定了用戶證書信任列表
                if (configuration.SecurityConfiguration.TrustedUserCertificates != null &&
                    configuration.SecurityConfiguration.UserIssuerCertificates != null)
                {
                    CertificateValidator certificateValidator = new();
                    certificateValidator.Update(configuration.SecurityConfiguration).Wait();

                    certificateValidator.Update(configuration.SecurityConfiguration.UserIssuerCertificates,
                        configuration.SecurityConfiguration.TrustedUserCertificates,
                        configuration.SecurityConfiguration.RejectedCertificateStore);

                    //為用戶證書設置自定義驗證器
                    m_userCertificateValidator = certificateValidator.GetChannelValidator();
                }
            }
        }
    }

    /// <summary>
    /// 當客戶端嘗試更改其用戶身份時調用
    /// </summary>
    private void SessionManager_ImpersonateUser(Session session, ImpersonateEventArgs args)
    {
        //檢查用戶名令牌
        if (args.NewIdentity is UserNameIdentityToken userNameToken)
        {
            args.Identity = VerifyPassword(userNameToken);

            //為接受的用戶/密碼身份驗證設置 AuthenticatedUser 角色
            args.Identity.GrantedRoleIds.Add(ObjectIds.WellKnownRole_AuthenticatedUser);

            if (args.Identity is SystemConfigurationIdentity)
            {
                //為有權配置服務器的用戶設置 ConfigureAdmin 角色
                args.Identity.GrantedRoleIds.Add(ObjectIds.WellKnownRole_ConfigureAdmin);
                args.Identity.GrantedRoleIds.Add(ObjectIds.WellKnownRole_SecurityAdmin);
            }

            return;
        }

        //檢查 x509 用戶令牌
        if (args.NewIdentity is X509IdentityToken x509Token)
        {
            VerifyUserTokenCertificate(x509Token.Certificate);
            args.Identity = new UserIdentity(x509Token);
            Utils.Trace("X509 Token Accepted: {0}", args.Identity.DisplayName);

            //為接受的證書身份驗證設置 AuthenticatedUser 角色
            args.Identity.GrantedRoleIds.Add(ObjectIds.WellKnownRole_AuthenticatedUser);

            return;
        }

        //允許匿名身份驗證並為此身份驗證設置匿名角色
        args.Identity = new UserIdentity();
        args.Identity.GrantedRoleIds.Add(ObjectIds.WellKnownRole_Anonymous);
    }

    /// <summary>
    /// 驗證用戶名令牌的密碼
    /// </summary>
    private IUserIdentity VerifyPassword(UserNameIdentityToken userNameToken)
    {
        var userName = userNameToken.UserName;

        var password = userNameToken.DecryptedPassword;

        if (string.IsNullOrEmpty(userName))
        {
            //不接受空用戶名
            throw ServiceResultException.Create(StatusCodes.BadIdentityTokenInvalid,
                "Security token is not a valid username token. An empty username is not accepted.");
        }

        if (string.IsNullOrEmpty(password))
        {
            //不接受空密碼
            throw ServiceResultException.Create(StatusCodes.BadIdentityTokenRejected,
                "Security token is not a valid username token. An empty password is not accepted.");
        }

        //有權配置服務器的用戶
        if (userName == "sysadmin" && password == "demo")
        {
            return new SystemConfigurationIdentity(new UserIdentity(userNameToken));
        }

        //CTT驗證標準用戶
        if (!(userName == "user1" && password == "password" || userName == "user2" && password == "password1"))
        {
            //使用默認文本構造翻譯對象
            TranslationInfo info = new("InvalidPassword", "en-US", "Invalid username or password.", userName);

            //使用供應商定義的子代碼創建異常
            throw new ServiceResultException(new ServiceResult(StatusCodes.BadUserAccessDenied, "InvalidPassword", LoadServerProperties().ProductUri, new LocalizedText(info)));
        }

        return new UserIdentity(userNameToken);
    }

    /// <summary>
    /// 驗證證書用戶令牌是否可信
    /// </summary>
    private void VerifyUserTokenCertificate(X509Certificate2 certificate)
    {
        try
        {
            if (m_userCertificateValidator != null)
            {
                m_userCertificateValidator.Validate(certificate);
            }
            else
            {
                CertificateValidator.Validate(certificate);
            }
        }
        catch (Exception e)
        {
            TranslationInfo info;

            StatusCode result = StatusCodes.BadIdentityTokenRejected;

            if (e is ServiceResultException se && se.StatusCode == StatusCodes.BadCertificateUseNotAllowed)
            {
                info = new TranslationInfo("InvalidCertificate", "en-US", "'{0}' is an invalid user certificate.", certificate.Subject);

                result = StatusCodes.BadIdentityTokenInvalid;
            }
            else
            {
                //使用默認文本構造翻譯對象
                info = new TranslationInfo("UntrustedCertificate", "en-US", "'{0}' is not a trusted user certificate.", certificate.Subject);
            }

            //使用供應商定義的子代碼創建異常
            throw new ServiceResultException(new ServiceResult(result, info.Key, LoadServerProperties().ProductUri, new LocalizedText(info)));
        }
    }

    #endregion

    private ICertificateValidator m_userCertificateValidator;
}