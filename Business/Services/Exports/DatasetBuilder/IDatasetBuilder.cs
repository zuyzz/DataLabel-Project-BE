using System;
using System.Threading.Tasks;

namespace DataLabelProject.Business.Services.Exports.DatasetBuilder;

public interface IDatasetBuilder
{
    Task<ExportDataset> BuildFromConsensus(Guid projectId);
}