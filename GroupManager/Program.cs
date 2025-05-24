namespace Group_Manager;

public class Program
{
    private static readonly CPHInline _cph = null;

    public static void Main()
    {
        try
        {
            _cph?.Init();
            _ = _cph?.Execute();
        }
        finally
        {
            _cph?.Dispose();
        }
    }
}
