using System.Linq.Expressions;

namespace Newsletters.Domain.Specifications;

public interface ISpecification<T>
{
    IReadOnlyList<Expression<Func<T, bool>>> Criteria { get; }
    IReadOnlyList<string> Includes { get; }
    Expression<Func<T, object?>>? OrderBy { get; }
    bool OrderDescending { get; }
    int? Skip { get; }
    int? Take { get; }
}

public abstract class Specification<T> : ISpecification<T>
{
    private readonly List<Expression<Func<T, bool>>> _criteria = [];
    private readonly List<string> _includes = [];

    public IReadOnlyList<Expression<Func<T, bool>>> Criteria => _criteria;
    public IReadOnlyList<string> Includes => _includes;
    public Expression<Func<T, object?>>? OrderBy { get; private set; }
    public bool OrderDescending { get; private set; }
    public int? Skip { get; private set; }
    public int? Take { get; private set; }

    protected void AddCriteria(Expression<Func<T, bool>> criteria) => _criteria.Add(criteria);
    protected void AddInclude(string navigationPath) => _includes.Add(navigationPath);
    protected void SetOrder(Expression<Func<T, object?>> orderBy, bool descending)
    {
        OrderBy = orderBy;
        OrderDescending = descending;
    }
    protected void SetPagination(int skip, int take)
    {
        Skip = skip;
        Take = take;
    }
}
