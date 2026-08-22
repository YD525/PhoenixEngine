using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Text.RegularExpressions;

namespace PhoenixEngine.ADO
{
    /// <summary>
    /// Creates SQLite parameters and constrains SQL fragments that cannot be represented by parameters.
    /// </summary>
    internal static class SqliteSql
    {
        internal const string LanguageFilter = "WHERE [From] = @from AND [To] = @to";
        internal const string SourceLanguageFilter =
            "WHERE [Source] = @source AND [From] = @from AND [To] = @to";

        private static readonly HashSet<string> AllowedIdentifiers = new HashSet<string>(
            StringComparer.Ordinal)
        {
            "AdvancedDictionary",
            "AdvancedDictionary_Old",
            "ChineseVariantMap",
            "CloudTranslation",
            "FontColors",
            "LocalTranslation",
            "RecordsHistory",
            "UniqueKeys",
            "Words"
        };

        private static readonly Regex ParameterName = new Regex(
            "^@[A-Za-z][A-Za-z0-9_]*$",
            RegexOptions.CultureInvariant);

        /// <summary>Creates a named parameter while preserving <see langword="null"/> as SQL NULL.</summary>
        /// <param name="name">The fixed parameter name used by the command text.</param>
        /// <param name="value">The data value to bind.</param>
        /// <returns>A caller-owned parameter ready to add to one command.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is not a valid parameter name.</exception>
        internal static SQLiteParameter Parameter(string name, object value)
        {
            if (string.IsNullOrEmpty(name) || !ParameterName.IsMatch(name))
                throw new ArgumentException("SQLite parameter names must use a fixed @name token.", nameof(name));

            return new SQLiteParameter(name, value ?? DBNull.Value);
        }

        /// <summary>Quotes an identifier selected from the product schema allowlist.</summary>
        /// <param name="identifier">The table identifier required by a fixed schema operation.</param>
        /// <returns>The allow-listed identifier enclosed in SQLite identifier quotes.</returns>
        /// <exception cref="ArgumentException">Thrown when the identifier is not part of the product schema.</exception>
        internal static string QuoteIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier) || !AllowedIdentifiers.Contains(identifier))
                throw new ArgumentException("The SQLite identifier is not part of the product schema.", nameof(identifier));

            return "[" + identifier + "]";
        }

        /// <summary>Returns a pagination filter only when it is one of the fixed supported templates.</summary>
        /// <param name="filter">The SQL filter template without embedded data values.</param>
        /// <returns>The normalized, allow-listed filter.</returns>
        /// <exception cref="ArgumentException">Thrown when the filter is not a fixed supported template.</exception>
        internal static string RequirePaginationFilter(string filter)
        {
            string candidate = (filter ?? string.Empty).Trim();
            if (candidate.Length == 0 ||
                string.Equals(candidate, LanguageFilter, StringComparison.Ordinal) ||
                string.Equals(candidate, SourceLanguageFilter, StringComparison.Ordinal))
            {
                return candidate;
            }

            throw new ArgumentException("The SQLite pagination filter is not allow-listed.", nameof(filter));
        }
    }
}
