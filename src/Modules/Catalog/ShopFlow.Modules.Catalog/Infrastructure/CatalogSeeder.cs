using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using ShopFlow.BuildingBlocks.Database;

namespace ShopFlow.Modules.Catalog.Infrastructure;

internal class CatalogSeeder : IModuleSeeder
{
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<CatalogSeeder> _logger;

    public CatalogSeeder(IAmazonS3 s3Client, ILogger<CatalogSeeder> logger)
    {
        _s3Client = s3Client;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var bucketName = "product-images";
        try
        {
            var exists = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, bucketName);
            if (!exists)
            {
                var request = new PutBucketRequest
                {
                    BucketName = bucketName,
                    UseClientRegion = true
                };

                await _s3Client.PutBucketAsync(request, cancellationToken);
                
                var policy = $"{{\"Version\":\"2012-10-17\",\"Statement\":[{{\"Action\":[\"s3:GetObject\"],\"Effect\":\"Allow\",\"Principal\":{{\"AWS\":[\"*\"]}},\"Resource\":[\"arn:aws:s3:::{bucketName}/*\"]}}]}}";
                await _s3Client.PutBucketPolicyAsync(new PutBucketPolicyRequest { BucketName = bucketName, Policy = policy }, cancellationToken);

                _logger.LogInformation($"Created bucket {bucketName}.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating bucket {bucketName}");
        }
    }
}
