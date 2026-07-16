namespace ReceiptFlow.Application.Abstractions.Authentication;

public interface ICurrentUser
{
	string UserId { get; }

	bool IsAuthenticated { get; }
}
