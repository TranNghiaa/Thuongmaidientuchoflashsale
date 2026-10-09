using System;
using Npgsql;

namespace ShopFlow.BuildingBlocks.Database;

public static class PostgresErrors
{
    public static bool IsUniqueViolation(Exception exception, string? constraintName = null)
    {
        var pgEx = exception as PostgresException ?? exception.InnerException as PostgresException;
        
        if (pgEx != null && pgEx.SqlState == "23505")
        {
            if (constraintName == null) return true;
            return string.Equals(pgEx.ConstraintName, constraintName, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
