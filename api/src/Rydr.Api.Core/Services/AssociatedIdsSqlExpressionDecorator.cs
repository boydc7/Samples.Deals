using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Dto.Interfaces;
using ServiceStack.OrmLite;

namespace Rydr.Api.Core.Services
{
    public class AssociatedIdsSqlExpressionDecorator : IDecorateSqlExpressionService
    {
        private readonly IAssociationService _associationService;
        private readonly IOrmLiteDialectProvider _dialect;

        public AssociatedIdsSqlExpressionDecorator(IRydrDataService rydrDataService,
                                                   IAssociationService associationService)
        {
            _associationService = associationService;
            _dialect = rydrDataService.Dialect;
        }

        public async Task DecorateAsync<TRequest, TFrom>(TRequest request, SqlExpression<TFrom> query)
        {
            if (!(request is IQueryAssociatedRecords qar) ||
                !qar.RecordId.HasValue || !qar.RecordType.HasValue)
            {
                return;
            }

            var associatedIds = await _associationService.GetAssociatedIdsAsync(qar.RecordId.Value, qar.DecorateRecordType, qar.RecordType.Value)
                                                         .Take(100)
                                                         .ToList(100);

            var ids = new SqlInValues(associatedIds, _dialect);
            var idField = SqlHelpers.GetOrmLiteFieldAliasName<TFrom>("Id");
            var tableName = SqlHelpers.GetTableName<TFrom>(_dialect);

            query.And(string.Concat(tableName, ".", idField,
                                    ids.Count <= 0
                                        ? string.Concat(" = ", int.MinValue)
                                        : string.Concat(" IN(", ids.ToSqlInString(), ")")));
        }
    }
}
