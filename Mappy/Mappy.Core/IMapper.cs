namespace Mappy.Core
{
    public interface IMapper<in TSource, TTarget>
    {
        TTarget Map(TSource source, TTarget target);
    }
}