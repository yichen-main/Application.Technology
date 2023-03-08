using KeyValuePair = Opc.Ua.KeyValuePair;
using TypeInfo = Opc.Ua.TypeInfo;

namespace Axis.OpcUa.Station.Services
{
    /// <summary>
    /// 以下備註中  測點即代表最葉子級節點
    /// 目前設計是 只有測點有數據  其餘節點都是目錄
    /// </summary>
    public class NodeManager : CustomNodeManager2
    {
        /// <summary>
        /// 配置修改次數  主要用來識別菜單樹是否有變動  如果發生變動則修改菜單樹對應節點  測點的實時數據變化不算在內
        /// </summary>
        private int cfgCount = -1;
        private IList<IReference>? _references;

        /// <summary>
        /// 測點集合,實時數據刷新時,直接從字典中取出對應的測點,修改值即可
        /// </summary>
        private readonly Dictionary<string, BaseDataVariableState> _nodeDic = new();

        /// <summary>
        /// 目錄集合,修改菜單樹時需要(我們需要知道哪些菜單需要修改,哪些需要新增,哪些需要刪除)
        /// </summary>
        private readonly Dictionary<string, FolderState> _folderDic = new();

        public NodeManager(IServerInternal server, ApplicationConfiguration configuration) : base(server, configuration, "http://opcfoundation.org/Quickstarts/ReferenceApplications")
        {
        }

        /// <summary>
        /// 重寫NodeId生成方式(目前採用'_'分隔,如需更改,請修改此方法)
        /// </summary>
        /// <param name="context"></param>
        /// <param name="node"></param>
        /// <returns></returns>
        public override NodeId New(ISystemContext context, NodeState node)
        {
            if (node is BaseInstanceState instance && instance.Parent != null)
            {
                if (instance.Parent.NodeId.Identifier is string id)
                {
                    return new NodeId(id + "_" + instance.SymbolicName, instance.Parent.NodeId.NamespaceIndex);
                }
            }
            return node.NodeId;
        }

        /// <summary>
        /// 重寫獲取節點句柄的方法
        /// </summary>
        /// <param name="context"></param>
        /// <param name="nodeId"></param>
        /// <param name="cache"></param>
        /// <returns></returns>
        protected override NodeHandle? GetManagerHandle(ServerSystemContext context, NodeId nodeId, IDictionary<NodeId, NodeState> cache)
        {
            lock (Lock)
            {
                //快速排除不在命名空間中的節點。
                if (!IsNodeIdInNamespace(nodeId))
                {
                    return null;
                }

                if (!PredefinedNodes.TryGetValue(nodeId, out NodeState? node))
                {
                    return null;
                }

                NodeHandle handle = new()
                {
                    NodeId = nodeId,
                    Node = node,
                    Validated = true
                };
                return handle;
            }
        }

        /// <summary>
        /// 重寫節點的驗證方式
        /// </summary>
        /// <param name="context"></param>
        /// <param name="handle"></param>
        /// <param name="cache"></param>
        /// <returns></returns>
        protected override NodeState? ValidateNode(ServerSystemContext context, NodeHandle handle, IDictionary<NodeId, NodeState> cache)
        {
            //如果沒有根則無效。
            if (handle == null)
            {
                return null;
            }

            //檢查之前是否經過驗證。
            if (handle.Validated)
            {
                return handle.Node;
            }
            // TBD
            return null;
        }

        /// <summary>
        /// 重寫創建基礎目錄
        /// </summary>
        /// <param name="externalReferences"></param>
        public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
        {
            lock (Lock)
            {
                if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out _references))
                {
                    externalReferences[ObjectIds.ObjectsFolder] = _references = new List<IReference>();
                }
                try
                {
                    List<PathNode> nodes = new()
                    {
                        new()
                        {
                            NodeId = 1,
                            IsTerminal = false,
                            NodeName = "基礎模型",
                            NodePath = "1",
                            NodeType = NodeType.Scada,
                            ParentPath = ""
                        },
                        new()
                        {
                            NodeId = 11,
                            IsTerminal = false,
                            NodeName = "子目錄-1",
                            NodePath = "11",
                            NodeType = NodeType.Channel,
                            ParentPath = "1"
                        },
                        new()
                        {
                            NodeId = 12,
                            IsTerminal = false,
                            NodeName = "子目錄-1",
                            NodePath = "12",
                            NodeType = NodeType.Device,
                            ParentPath = "1"
                        },
                        new()
                        {
                            NodeId = 111,
                            IsTerminal = true,
                            NodeName = "葉子節點-1",
                            NodePath = "111",
                            NodeType = NodeType.Measure,
                            ParentPath = "11"
                        },
                        new()
                        {
                            NodeId = 112,
                            IsTerminal = true,
                            NodeName = "葉子節點-2",
                            NodePath = "112",
                            NodeType = NodeType.Measure,
                            ParentPath = "11"
                        },
                        new()
                        {
                            NodeId = 113,
                            IsTerminal = true,
                            NodeName = "葉子節點-3",
                            NodePath = "113",
                            NodeType = NodeType.Measure,
                            ParentPath = "11"
                        },
                        new()
                        {
                            NodeId = 114,
                            IsTerminal = true,
                            NodeName = "葉子節點-4",
                            NodePath = "114",
                            NodeType = NodeType.Measure,
                            ParentPath = "11"
                        },
                        new()
                        {
                            NodeId = 121,
                            IsTerminal = true,
                            NodeName = "葉子節點-1",
                            NodePath = "121",
                            NodeType = NodeType.Measure,
                            ParentPath = "12"
                        },
                        new()
                        {
                            NodeId = 122,
                            IsTerminal = true,
                            NodeName = "葉子節點-2",
                            NodePath = "122",
                            NodeType = NodeType.Measure,
                            ParentPath = "12"
                        }
                    };

                    //開始創建節點的菜單樹
                    GeneraterNodes(nodes, _references);

                    //實時更新測點的數據
                    UpdateVariableValue();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("調用接口初始化觸發異常:" + ex.Message);
                    Console.ResetColor();
                }
            }
        }

        /// <summary>
        /// 生成根節點(由於根節點需要特殊處理,此處單獨出來一個方法)
        /// </summary>
        /// <param name="nodes"></param>
        /// <param name="references"></param>
        private void GeneraterNodes(IEnumerable<PathNode> nodes, IList<IReference> references)
        {
            var list = nodes.Where(d => d.NodeType == NodeType.Scada);
            foreach (var item in list)
            {
                try
                {
                    FolderState root = CreateFolder(null, item.NodePath, item.NodeName);
                    root.AddReference(ReferenceTypes.Organizes, true, ObjectIds.ObjectsFolder);
                    references.Add(new NodeStateReference(ReferenceTypes.Organizes, false, root.NodeId));
                    root.EventNotifier = EventNotifiers.SubscribeToEvents;
                    AddRootNotifier(root);
                    CreateNodes(nodes, root, item.NodePath);
                    _folderDic.Add(item.NodePath, root);

                    //添加引用關係
                    AddPredefinedNode(SystemContext, root);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("創建 OPC UA 根節點觸發異常:" + ex.Message);
                    Console.ResetColor();
                }
            }
        }

        /// <summary>
        /// 遞歸創建子節點(包括創建目錄和測點)
        /// </summary>
        /// <param name="nodes"></param>
        /// <param name="parent"></param>
        private void CreateNodes(IEnumerable<PathNode> nodes, FolderState parent, string parentPath)
        {
            var list = nodes.Where(d => d.ParentPath == parentPath);
            foreach (var node in list)
            {
                try
                {
                    if (!node.IsTerminal)
                    {
                        FolderState folder = CreateFolder(parent, node.NodePath, node.NodeName);
                        _folderDic.Add(node.NodePath, folder);
                        CreateNodes(nodes, folder, node.NodePath);
                    }
                    else
                    {
                        BaseDataVariableState variable = CreateVariable(parent, node.NodePath, node.NodeName, DataTypeIds.Double, ValueRanks.Scalar);

                        //此處需要注意  目錄字典是以目錄路徑作為KEY 而 測點字典是以測點ID作為KEY  為了方便更新實時數據
                        _nodeDic.Add(node.NodeId.ToString(), variable);
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("創建 OPC UA 子節點觸發異常:" + ex.Message);
                    Console.ResetColor();
                }
            }
        }

        /// <summary>
        /// 創建目錄
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="path"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        private FolderState CreateFolder(NodeState? parent, string path, string name)
        {
            FolderState folder = new(parent)
            {
                SymbolicName = name,
                ReferenceTypeId = ReferenceTypes.Organizes,
                TypeDefinitionId = ObjectTypeIds.FolderType,
                NodeId = new NodeId(path, NamespaceIndex),
                BrowseName = new QualifiedName(path, NamespaceIndex),
                DisplayName = new LocalizedText("en", name),
                WriteMask = AttributeWriteMask.None,
                UserWriteMask = AttributeWriteMask.None,
                EventNotifier = EventNotifiers.None
            };

            parent?.AddChild(folder);
            return folder;
        }

        /// <summary>
        /// 創建節點
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="path"></param>
        /// <param name="name"></param>
        /// <param name="dataType"></param>
        /// <param name="valueRank"></param>
        /// <returns></returns>
        private BaseDataVariableState CreateVariable(NodeState parent, string path, string name, NodeId dataType, int valueRank)
        {
            BaseDataVariableState variable = new(parent)
            {
                SymbolicName = name,
                ReferenceTypeId = ReferenceTypes.Organizes,
                TypeDefinitionId = VariableTypeIds.BaseDataVariableType,
                NodeId = new NodeId(path, NamespaceIndex),
                BrowseName = new QualifiedName(path, NamespaceIndex),
                DisplayName = new LocalizedText("en", name),
                WriteMask = AttributeWriteMask.DisplayName | AttributeWriteMask.Description,
                UserWriteMask = AttributeWriteMask.DisplayName | AttributeWriteMask.Description,
                DataType = dataType,
                ValueRank = valueRank,
                AccessLevel = AccessLevels.CurrentReadOrWrite,
                UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                Historizing = false,
                //variable.Value = GetNewValue(variable);
                StatusCode = StatusCodes.Good,
                Timestamp = DateTime.Now,
                OnWriteValue = OnWriteDataValue,
                OnReadValue = OnReadDataValue
            };

            if (valueRank == ValueRanks.OneDimension)
            {
                variable.ArrayDimensions = new ReadOnlyList<uint>(new List<uint> { 0 });
            }
            else if (valueRank == ValueRanks.TwoDimensions)
            {
                variable.ArrayDimensions = new ReadOnlyList<uint>(new List<uint> { 0, 0 });
            }

            parent?.AddChild(variable);
            return variable;
        }

        private ServiceResult OnReadDataValue(ISystemContext context, NodeState node, NumericRange indexRange, QualifiedName dataEncoding, ref object value, ref StatusCode statusCode, ref DateTime timestamp)
        {

            return ServiceResult.Good;
        }

        /// <summary>
        /// 實時更新節點數據
        /// </summary>
        public void UpdateVariableValue()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        /*
                         * 此處僅作示例代碼  所以不修改節點樹 故將UpdateNodesAttribute()方法跳過
                         * 在實際業務中  請根據自身的業務需求決定何時修改節點菜單樹
                         */
                        int count = 0;

                        //配置發生更改時,重新生成節點樹
                        if (count > 0 && count != cfgCount)
                        {
                            cfgCount = count;
                            List<PathNode> nodes = new();
                            /*
                             * 此處有想過刪除整個菜單樹,然後重建 保證各個NodeId仍與原來的一直
                             * 但是 後來發現這樣會導致原來的客戶端訂閱信息丟失  無法獲取訂閱數據
                             * 所以  只能一級級的檢查節點  然後修改屬性
                             */
                            UpdateNodesAttribute(nodes);
                        }

                        //模擬獲取實時數據
                        BaseDataVariableState? node = null;

                        /*
                         * 在實際業務中應該是根據對應的標識來更新固定節點的數據
                         * 這裡  我偷個懶  全部測點都更新為一個新的隨機數
                         */
                        foreach (var item in _nodeDic)
                        {
                            node = item.Value;
                            node.Value = RandomLibrary.GetRandomInt(0, 99);
                            node.Timestamp = DateTime.Now;

                            //變更標識  只有執行了這一步,訂閱的客戶端才會收到新的數據
                            node.ClearChangeMasks(SystemContext, false);
                        }

                        //1秒更新一次
                        Thread.Sleep(1000 * 1);
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("更新 OPC UA 節點數據觸發異常:" + ex.Message);
                        Console.ResetColor();
                    }
                }
            });
        }

        /// <summary>
        /// 修改節點樹(添加節點,刪除節點,修改節點名稱)
        /// </summary>
        /// <param name="nodes"></param>
        public void UpdateNodesAttribute(List<PathNode> nodes)
        {
            //修改或創建根節點
            var scadas = nodes.Where(d => d.NodeType == NodeType.Scada);
            foreach (var item in scadas)
            {
                if (!_folderDic.TryGetValue(item.NodePath, out FolderState? scadaNode))
                {
                    //如果根節點都不存在  那麼整個樹都需要創建
                    FolderState root = CreateFolder(null, item.NodePath, item.NodeName);
                    root.AddReference(ReferenceTypes.Organizes, true, ObjectIds.ObjectsFolder);
                    _references?.Add(new NodeStateReference(ReferenceTypes.Organizes, false, root.NodeId));
                    root.EventNotifier = EventNotifiers.SubscribeToEvents;
                    AddRootNotifier(root);
                    CreateNodes(nodes, root, item.NodePath);
                    _folderDic.Add(item.NodePath, root);
                    AddPredefinedNode(SystemContext, root);
                    continue;
                }
                else
                {
                    scadaNode.DisplayName = item.NodeName;
                    scadaNode.ClearChangeMasks(SystemContext, false);
                }
            }

            //修改或創建目錄(此處設計為可以有多級目錄,上面是演示數據,所以我只寫了三級,事實上更多級也是可以的)
            var folders = nodes.Where(d => d.NodeType != NodeType.Scada && !d.IsTerminal);
            foreach (var item in folders)
            {
                if (!_folderDic.TryGetValue(item.NodePath, out FolderState? folder))
                {
                    var par = GetParentFolderState(nodes, item);
                    folder = CreateFolder(par, item.NodePath, item.NodeName);
                    AddPredefinedNode(SystemContext, folder);
                    par?.ClearChangeMasks(SystemContext, false);
                    _folderDic.Add(item.NodePath, folder);
                }
                else
                {
                    folder.DisplayName = item.NodeName;
                    folder.ClearChangeMasks(SystemContext, false);
                }
            }

            //修改或創建測點
            //這裡我的數據結構採用IsTerminal來代表是否是測點  實際業務中可能需要根據自身需要調整
            var paras = nodes.Where(d => d.IsTerminal);
            foreach (var item in paras)
            {
                if (_nodeDic.TryGetValue(item.NodeId.ToString(), out BaseDataVariableState? node))
                {
                    node.DisplayName = item.NodeName;
                    node.Timestamp = DateTime.Now;
                    node.ClearChangeMasks(SystemContext, false);
                }
                else
                {
                    if (_folderDic.TryGetValue(item.ParentPath, out FolderState? folder))
                    {
                        node = CreateVariable(folder, item.NodePath, item.NodeName, DataTypeIds.Double, ValueRanks.Scalar);
                        AddPredefinedNode(SystemContext, node);
                        folder.ClearChangeMasks(SystemContext, false);
                        _nodeDic.Add(item.NodeId.ToString(), node);
                    }
                }
            }

            /*
             * 將新獲取到的菜單列表與原列表對比
             * 如果新菜單列表中不包含原有的菜單  
             * 則說明這個菜單被刪除了  這裡也需要刪除
             */
            List<string> folderPath = _folderDic.Keys.ToList();
            List<string> nodePath = _nodeDic.Keys.ToList();
            var remNode = nodePath.Except(nodes.Where(d => d.IsTerminal).Select(d => d.NodeId.ToString()));
            foreach (var str in remNode)
            {
                if (_nodeDic.TryGetValue(str, out BaseDataVariableState? node))
                {
                    var parent = node.Parent;
                    parent.RemoveChild(node);
                    _nodeDic.Remove(str);
                }
            }
            var remFolder = folderPath.Except(nodes.Where(d => !d.IsTerminal).Select(d => d.NodePath));
            foreach (string str in remFolder)
            {
                if (_folderDic.TryGetValue(str, out FolderState? folder))
                {
                    var parent = folder.Parent;
                    if (parent != null)
                    {
                        parent.RemoveChild(folder);
                        _folderDic.Remove(str);
                    }
                    else
                    {
                        RemoveRootNotifier(folder);
                        RemovePredefinedNode(SystemContext, folder, new List<LocalReference>());
                    }
                }
            }
        }

        /// <summary>
        /// 創建父級目錄(請確保對應的根目錄已創建)
        /// </summary>
        /// <param name="nodes"></param>
        /// <param name="currentNode"></param>
        /// <returns></returns>
        public FolderState? GetParentFolderState(IEnumerable<PathNode> nodes, PathNode currentNode)
        {
            if (!_folderDic.TryGetValue(currentNode.ParentPath, out FolderState? folder))
            {
                var parent = nodes.Where(d => d.NodePath == currentNode.ParentPath).FirstOrDefault();
                if (!string.IsNullOrEmpty(parent.ParentPath))
                {
                    var pFol = GetParentFolderState(nodes, parent);
                    folder = CreateFolder(pFol, parent.NodePath, parent.NodeName);
                    pFol?.ClearChangeMasks(SystemContext, false);
                    AddPredefinedNode(SystemContext, folder);
                    _folderDic.Add(currentNode.ParentPath, folder);
                }
            }
            return folder;
        }

        /// <summary>
        /// 客戶端寫入值時觸發(綁定到節點的寫入事件上)
        /// </summary>
        /// <param name="context"></param>
        /// <param name="node"></param>
        /// <param name="indexRange"></param>
        /// <param name="dataEncoding"></param>
        /// <param name="value"></param>
        /// <param name="statusCode"></param>
        /// <param name="timestamp"></param>
        /// <returns></returns>
        private ServiceResult OnWriteDataValue(ISystemContext context, NodeState node, NumericRange indexRange, QualifiedName dataEncoding,
            ref object value, ref StatusCode statusCode, ref DateTime timestamp)
        {
            BaseDataVariableState? variable = node as BaseDataVariableState;
            try
            {
                //驗證數據類型
                TypeInfo typeInfo = TypeInfo.IsInstanceOfDataType(
                    value,
                    variable?.DataType,
                    variable.ValueRank,
                    context.NamespaceUris,
                    context.TypeTable);

                if (typeInfo == null || typeInfo == TypeInfo.Unknown)
                {
                    return StatusCodes.BadTypeMismatch;
                }
                if (typeInfo.BuiltInType == BuiltInType.Double)
                {
                    double number = Convert.ToDouble(value);
                    value = TypeInfo.Cast(number, typeInfo.BuiltInType);
                }
                return ServiceResult.Good;
            }
            catch (Exception)
            {
                return StatusCodes.BadTypeMismatch;
            }
        }

        /// <summary>
        /// 讀取歷史數據
        /// </summary>
        /// <param name="context"></param>
        /// <param name="details"></param>
        /// <param name="timestampsToReturn"></param>
        /// <param name="releaseContinuationPoints"></param>
        /// <param name="nodesToRead"></param>
        /// <param name="results"></param>
        /// <param name="errors"></param>
        public override void HistoryRead(OperationContext context, HistoryReadDetails details, TimestampsToReturn timestampsToReturn, bool releaseContinuationPoints,
            IList<HistoryReadValueId> nodesToRead, IList<HistoryReadResult> results, IList<ServiceResult> errors)
        {
            //假設查詢歷史數據都是帶上時間範圍的
            if (details is not ReadProcessedDetails readDetail || readDetail.StartTime == DateTime.MinValue || readDetail.EndTime == DateTime.MinValue)
            {
                errors[0] = StatusCodes.BadHistoryOperationUnsupported;
                return;
            }
            for (int ii = 0; ii < nodesToRead.Count; ii++)
            {
                int sss = readDetail.StartTime.Millisecond;
                var res = sss + DateTime.Now.Millisecond;

                //這裡  返回的歷史數據可以是多種數據類型  請根據實際的業務來選擇
                KeyValuePair keyValue = new()
                {
                    Key = new QualifiedName(nodesToRead[ii].NodeId.Identifier.ToString()),
                    Value = res
                };
                results[ii] = new HistoryReadResult()
                {
                    StatusCode = StatusCodes.Good,
                    HistoryData = new ExtensionObject(keyValue)
                };
                errors[ii] = StatusCodes.Good;

                //切記,如果你已處理完了讀取歷史數據的操作,請將Processed設為true,這樣 OPC UA 類庫就知道你已經處理過了 不需要再進行檢查了
                nodesToRead[ii].Processed = true;
            }
        }
    }
}