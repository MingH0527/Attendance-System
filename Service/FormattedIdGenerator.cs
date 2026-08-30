using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace WebAndMobile.Services;

public static class FormattedIdGenerator
{
    public static async Task<string> NextAsync<TEntity>(
        DbSet<TEntity> set,
        Expression<Func<TEntity, string>> idSelector,
        string prefix,
        int digits = 4,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        List<string> persistedIds = await set
            .AsNoTracking()
            .Select(idSelector)
            .ToListAsync(cancellationToken);

        Func<TEntity, string> selector = idSelector.Compile();

        IEnumerable<string> allIds = persistedIds.Concat(
            set.Local
                .Select(selector)
                .Where(id => !string.IsNullOrWhiteSpace(id))
        );

        int maxNumber = 0;

        foreach (string id in allIds)
        {
            if (!id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string suffix = id[prefix.Length..];

            if (int.TryParse(suffix, out int number) &&
                number > maxNumber)
            {
                maxNumber = number;
            }
        }

        return $"{prefix}{(maxNumber + 1).ToString($"D{digits}")}";
    }
}
