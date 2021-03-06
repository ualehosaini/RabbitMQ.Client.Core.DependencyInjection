using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RabbitMQ.Client.Core.DependencyInjection.Filters;
using RabbitMQ.Client.Core.DependencyInjection.Services.Interfaces;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Client.Core.DependencyInjection.Services
{
    /// <inheritdoc/>
    public class MessageHandlingPipelineExecutingService : IMessageHandlingPipelineExecutingService
    {
        private readonly IMessageHandlingService _messageHandlingService;
        private readonly IEnumerable<IMessageHandlingFilter> _handlingFilters;
        private readonly IEnumerable<IMessageHandlingExceptionFilter> _exceptionFilters;

        public MessageHandlingPipelineExecutingService(
            IMessageHandlingService messageHandlingService,
            IEnumerable<IMessageHandlingFilter> handlingFilters,
            IEnumerable<IMessageHandlingExceptionFilter> exceptionFilters)
        {
            _messageHandlingService = messageHandlingService;
            _handlingFilters = handlingFilters ?? Enumerable.Empty<IMessageHandlingFilter>();
            _exceptionFilters = exceptionFilters ?? Enumerable.Empty<IMessageHandlingExceptionFilter>();
        }

        /// <inheritdoc/>
        public async Task Execute(BasicDeliverEventArgs eventArgs, IConsumingService consumingService)
        {
            try
            {
                await ExecutePipeline(eventArgs, consumingService).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                await ExecuteFailurePipeline(exception, eventArgs, consumingService).ConfigureAwait(false);
            }
        }

        private async Task ExecutePipeline(BasicDeliverEventArgs eventArgs, IConsumingService consumingService)
        {
            Func<BasicDeliverEventArgs, IConsumingService, Task> handle = _messageHandlingService.HandleMessageReceivingEvent;
            foreach (var filter in _handlingFilters.Reverse())
            {
                handle = filter.Execute(handle);
            }

            await handle(eventArgs, consumingService).ConfigureAwait(false);
        }

        private async Task ExecuteFailurePipeline(Exception exception, BasicDeliverEventArgs eventArgs, IConsumingService consumingService)
        {
            Func<Exception, BasicDeliverEventArgs, IConsumingService, Task> handle = _messageHandlingService.HandleMessageProcessingFailure;
            foreach (var filter in _exceptionFilters.Reverse())
            {
                handle = filter.Execute(handle);
            }

            await handle(exception, eventArgs, consumingService).ConfigureAwait(false);
        }
    }
}