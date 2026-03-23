namespace DataLabelProject.Business.Services.Consensus;

public interface IAgreementService
{
	double CalculateOverallScore(List<BoxCluster> clusters, int totalAnnotators);
	List<ConsensusBboxDto>? BuildConsensusBboxes(List<BoxCluster> clusters);
}

public class AgreementService : IAgreementService
{
	/// <summary>
	/// Majority vote: for each cluster, score = annotators voting majority label / total annotators in that cluster.
	/// Overall score = average across all clusters.
	/// </summary>
	public double CalculateOverallScore(List<BoxCluster> clusters, int totalAnnotators)
	{
		if (clusters.Count == 0 || totalAnnotators <= 0)
			return 0;

		var clusterScores = clusters.Select(cluster =>
		{
			var labelVotes = cluster.Members
				.GroupBy(m => m.Label)
				.Select(g => g.Select(m => m.AnnotatorId).Distinct().Count())
				.DefaultIfEmpty(0)
				.Max();

			var totalVoters = cluster.Members
				.Select(m => m.AnnotatorId)
				.Distinct()
				.Count();

			if (totalVoters == 0)
				return 0d;

			return (double)labelVotes / totalVoters;
		});

		return clusterScores.Average();
	}

	public List<ConsensusBboxDto>? BuildConsensusBboxes(List<BoxCluster> clusters)
	{
		var result = new List<ConsensusBboxDto>();

		foreach (var cluster in clusters)
		{
			var labelGroups = cluster.Members
				.GroupBy(m => m.Label)
				.Select(g => new
				{
					Label = g.Key,
					Members = g.ToList(),
					VoteCount = g.Select(m => m.AnnotatorId).Distinct().Count()
				})
				.OrderByDescending(g => g.VoteCount)
				.ThenBy(g => g.Label)
				.ToList();

			if (labelGroups.Count == 0)
				continue;

			var top = labelGroups[0];
			var isTie = labelGroups.Count > 1 && labelGroups[1].VoteCount == top.VoteCount;

			if (isTie)
				return null;

			result.Add(new ConsensusBboxDto
			{
				Label = top.Label,
				X = top.Members.Average(m => m.X),
				Y = top.Members.Average(m => m.Y),
				Width = top.Members.Average(m => m.Width),
				Height = top.Members.Average(m => m.Height),
				Support = top.VoteCount
			});
		}

		return result;
	}
}
