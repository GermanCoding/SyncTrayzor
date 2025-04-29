﻿using FluentValidation;
using Stylet;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncTrayzor.Utils
{
    public class FluentModelValidator<T> : IModelValidator<T>
    {
        private readonly IValidator<T> validator;
        private T subject;

        public FluentModelValidator(IValidator<T> validator)
        {
            this.validator = validator;
        }

        public void Initialize(object subject)
        {
            this.subject = (T)subject;
        }

        public async Task<IEnumerable<string>> ValidatePropertyAsync(string propertyName)
        {
            // If someone's calling us synchronously, and ValidationAsync does not complete synchronously,
            // we'll deadlock unless we continue on another thread.
            var result = await validator.ValidateAsync(subject, options => options.IncludeProperties(propertyName))
                .ConfigureAwait(false);
            return result.Errors.Select(x => x.ErrorMessage);
        }

        public async Task<Dictionary<string, IEnumerable<string>>> ValidateAllPropertiesAsync()
        {
            // If someone's calling us synchronously, and ValidationAsync does not complete synchronously,
            // we'll deadlock unless we continue on another thread.
            return (await validator.ValidateAsync(subject).ConfigureAwait(false))
                .Errors.GroupBy(x => x.PropertyName)
                .ToDictionary(x => x.Key, x => x.Select(failure => failure.ErrorMessage));
        }
    }
}
