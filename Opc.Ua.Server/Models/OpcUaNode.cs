namespace Axis.OpcUa.Station.Models;
public enum NodeType
{
    /// <summary>
    /// 根節點
    /// </summary>
    Scada = 1,

    /// <summary>
    /// 目錄
    /// </summary>
    Channel = 2,

    /// <summary>
    /// 目錄
    /// </summary>
    Device = 3,

    /// <summary>
    /// 測點
    /// </summary>
    Measure = 4
}
public readonly record struct PathNode
{
    /// <summary>
    /// 節點路徑(逐級拼接)
    /// </summary>
    public required string NodePath { get; init; }

    /// <summary>
    /// 父節點路徑(逐級拼接)
    /// </summary>
    public required string ParentPath { get; init; }

    /// <summary>
    /// 節點編號 (在我的業務系統中的節點編號並不完全唯一,但是所有測點Id都是不同的)
    /// </summary>
    public required int NodeId { get; init; }

    /// <summary>
    /// 節點名稱(展示名稱)
    /// </summary>
    public required string NodeName { get; init; }

    /// <summary>
    /// 是否端點(最底端子節點)
    /// </summary>
    public required bool IsTerminal { get; init; }

    /// <summary>
    /// 節點類型
    /// </summary>
    public required NodeType NodeType { get; init; }
}