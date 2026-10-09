using System;
using Npgsql;
using ShopFlow.BuildingBlocks.Database;

namespace ShopFlow.UnitTests.BuildingBlocks;

public class PostgresErrorsTests
{
    [Fact]
    public void IsUniqueViolation_ReturnsTrue_WhenPostgresExceptionHas23505()
    {
        // Arrange
#pragma warning disable SYSLIB0050
        var pgEx = (PostgresException)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(PostgresException));
#pragma warning restore SYSLIB0050
        SetBackingField(pgEx, "SqlState", "23505");
        SetBackingField(pgEx, "ConstraintName", "my_constraint");
        
        var ex = new Exception("wrapper", pgEx);

        // Act
        var result1 = PostgresErrors.IsUniqueViolation(ex);
        var result2 = PostgresErrors.IsUniqueViolation(ex, "my_constraint");
        var result3 = PostgresErrors.IsUniqueViolation(ex, "OTHER_CONSTRAINT");

        // Assert
        Assert.True(result1);
        Assert.True(result2);
        Assert.False(result3);
    }

    [Fact]
    public void IsUniqueViolation_ReturnsFalse_WhenNot23505()
    {
        // Arrange
#pragma warning disable SYSLIB0050
        var pgEx = (PostgresException)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(PostgresException));
#pragma warning restore SYSLIB0050
        SetBackingField(pgEx, "SqlState", "12345");
        var ex = new Exception("wrapper", pgEx);

        // Act & Assert
        Assert.False(PostgresErrors.IsUniqueViolation(ex));
    }

    private static void SetBackingField(object obj, string propertyName, object value)
    {
        var field = obj.GetType().GetField($"<{propertyName}>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field?.SetValue(obj, value);
    }
}

