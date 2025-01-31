namespace MassTransit.JobService
{
    using System;
    using System.Runtime.Serialization;
    using System.Threading.Tasks;
    using Contracts.JobService;


    public class FinalizeJobConsumer<TJob> :
        IConsumer<FaultJob>,
        IConsumer<CompleteJob>
        where TJob : class
    {
        readonly string _jobConsumerTypeName;
        readonly IJobService _jobService;
        readonly Guid _jobTypeId;
        readonly JobOptions<TJob> _options;

        public FinalizeJobConsumer(IJobService jobService, JobOptions<TJob> options, Guid jobTypeId, string jobConsumerTypeName)
        {
            _jobService = jobService;
            _options = options;
            _jobTypeId = jobTypeId;
            _jobConsumerTypeName = jobConsumerTypeName;
        }

        public Task Consume(ConsumeContext<CompleteJob> context)
        {
            if (context.Message.JobTypeId != _jobTypeId)
                return Task.CompletedTask;

            _ = context.GetJob<TJob>() ?? throw new SerializationException($"The job could not be deserialized: {TypeCache<TJob>.ShortName}");

            return context.Publish<JobCompleted<TJob>>(context.Message);
        }

        public Task Consume(ConsumeContext<FaultJob> context)
        {
            var message = context.Message;
            if (message.JobTypeId != _jobTypeId)
                return Task.CompletedTask;

            var job = context.GetJob<TJob>() ?? throw new SerializationException($"The job could not be deserialized: {TypeCache<TJob>.ShortName}");

            using var jobContext = new ConsumeJobContext<TJob>(context, _jobService.InstanceAddress, message.JobId, message.AttemptId, message.RetryAttempt,
                job, _options.JobTimeout);

            return jobContext.NotifyFaulted(message.Duration ?? TimeSpan.Zero, _jobConsumerTypeName, new ExceptionInfoException(message.Exceptions));
        }
    }
}
