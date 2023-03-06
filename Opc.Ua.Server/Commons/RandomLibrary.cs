namespace Axis.OpcUa.Station.Commons;
public class RandomLibrary
{
    //private static string RandomString = "123456789abcdefghijkmnpqrstuvwxyz123456789ABCDEFGHIJKLMNPQRSTUVWXYZ123456789";
    private static readonly string RandomString = "123456789abcdefghijkmnpqrstuvwxyzABCDEFGHIJKLMNPQRSTUVWXYZ";
    private static readonly Random Random = new(DateTime.Now.Second);
    private static readonly Random _random = new();

    /// <summary>
    /// 產生隨機字符串
    /// </summary>
    /// <param name="length">字符串長度</param>
    /// <returns></returns>
    public static string GetRandomStr(int length)
    {
        string retValue = string.Empty;
        for (int i = 0; i < length; i++)
        {
            int r = Random.Next(0, RandomString.Length - 1);
            retValue += RandomString[r];
        }
        return retValue;
    }

    /// <summary>
    /// 產生隨機數整數
    /// </summary>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <returns></returns>
    public static int GetRandomInt(int min, int max) => Random.Next(min, max);

    /// <summary>
    /// 產生隨機小數
    /// </summary>
    /// <returns></returns>
    public static double GetRandomDouble() => _random.NextDouble();

    /// <summary>
    /// 隨機排序
    /// 因為數組是引用類型  所以不需要返回值
    /// 調用之後即改變原數組排序，直接使用即可
    /// </summary>
    /// <typeparam name="T">參數類型，必須為數組類型</typeparam>
    /// <param name="arr">陣列</param>
    public static void GetRandomSort<T>(T[] arr)
    {
        int count = arr.Length;
        for (int i = 0; i < count; i++)
        {
            int rn1 = GetRandomInt(0, arr.Length);
            int rn2 = GetRandomInt(0, arr.Length);
            T temp;
            temp = arr[rn1];
            arr[rn1] = arr[rn2];
            arr[rn2] = temp;
        }
    }
}