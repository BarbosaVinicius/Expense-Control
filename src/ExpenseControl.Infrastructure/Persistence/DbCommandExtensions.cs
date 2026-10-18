using System.Data.Common;

namespace ExpenseControl.Infrastructure.Persistence;

internal static class DbCommandExtensions
{
    /// <summary>Adds a named parameter; values are always sent as parameters, never concatenated into the SQL.</summary>
    public static void AddParameter(this DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
