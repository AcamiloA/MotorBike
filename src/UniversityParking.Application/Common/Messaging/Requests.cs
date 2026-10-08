using MediatR;
using UniversityParking.Application.Common.Results;

namespace UniversityParking.Application.Common.Messaging;

public interface ICommand : IRequest<Result>;
public interface ICommand<T> : IRequest<Result<T>>;
public interface IQuery<T> : IRequest<Result<T>>;
