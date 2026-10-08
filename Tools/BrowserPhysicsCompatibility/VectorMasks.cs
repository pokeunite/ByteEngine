using System.Numerics;
using System.Runtime.CompilerServices;
namespace ByteEngine.WebCompatibility;
public static class VectorMasks
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Vector<int> GreaterThan(Vector<float> a,Vector<float> b) => Vector.GreaterThan(a,b);
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Vector<int> LessThan(Vector<float> a,Vector<float> b) => Vector.LessThan(a,b);
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Vector<int> GreaterThanOrEqual(Vector<float> a,Vector<float> b) => Vector.GreaterThanOrEqual(a,b);
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Vector<int> LessThanOrEqual(Vector<float> a,Vector<float> b) => Vector.LessThanOrEqual(a,b);
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Vector<int> Equals(Vector<float> a,Vector<float> b) => Vector.Equals(a,b);
}
