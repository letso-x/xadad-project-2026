using Dapper;
using Microsoft.Data.Sqlite;
using MzukuluQMS.Api.Data.Repositories;
namespace MzukuluQMS.Api.Tests;

// Executes the production SELECT against the relevant live-schema column contract.
// SQLite checks column resolution/mapping here; PostgreSQL syntax is also checked
// separately against the live database using a read-only SELECT.
public sealed class ProjectFieldColumnContractTests
{
 [Theory][InlineData(14,101,true)][InlineData(13,101,false)][InlineData(14,102,false)]
 public async Task Validation_reads_a_field_without_a_field_revision_and_scopes_both_ids(long formId,long fieldId,bool found)
 {
  SQLitePCL.Batteries_V2.Init();
  await using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
  await connection.ExecuteAsync("""
   ATTACH DATABASE ':memory:' AS public;
   CREATE TABLE public."QCFormProjectField" (
    "QCFormID" INTEGER NOT NULL,"QCFormProjectFieldID" INTEGER NOT NULL,
    "FieldType" TEXT NOT NULL,"IsRequired" INTEGER NOT NULL,"Options" TEXT,"ValidationRules" TEXT);
   INSERT INTO public."QCFormProjectField" VALUES (14,101,'Reference',1,NULL,'{"maxLength":50}');
   """);
  var row=await connection.QuerySingleOrDefaultAsync<QCProjectFieldValidationRow>(QCFormRepository.ProjectFieldValidationSql,new{QCFormID=formId,QCFormProjectFieldID=fieldId});
  if(!found){Assert.Null(row);return;}
  Assert.NotNull(row);Assert.Equal(14,row.QCFormID);Assert.Equal(101,row.QCFormProjectFieldID);
  Assert.Equal("Reference",row.FieldType);Assert.True(row.IsRequired);Assert.Null(row.OptionsJson);Assert.Equal("{\"maxLength\":50}",row.ValidationRulesJson);
 }
}
