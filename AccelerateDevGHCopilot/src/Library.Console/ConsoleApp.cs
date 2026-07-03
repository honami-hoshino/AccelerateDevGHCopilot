using System;
using System.Linq;
using Library.ApplicationCore;
using Library.ApplicationCore.Entities;
using Library.ApplicationCore.Enums;
using Library.Console;
using Library.Infrastructure.Data;

public class ConsoleApp
{
    ConsoleState _currentState = ConsoleState.PatronSearch;

    List<Patron> matchingPatrons = new List<Patron>();

    Patron? selectedPatronDetails = null;
    Loan selectedLoanDetails = null!;

    IPatronRepository _patronRepository;
    ILoanRepository _loanRepository;
    ILoanService _loanService;
    IPatronService _patronService;
    JsonData _jsonData;

    public ConsoleApp(ILoanService loanService, IPatronService patronService, IPatronRepository patronRepository, ILoanRepository loanRepository, JsonData jsonData)
    {
        _patronRepository = patronRepository;
        _loanRepository = loanRepository;
        _loanService = loanService;
        _patronService = patronService;
        _jsonData = jsonData;
    }

    public async Task Run()
    {
        while (true)
        {
            switch (_currentState)
            {
                case ConsoleState.PatronSearch:
                    _currentState = await PatronSearch();
                    break;
                case ConsoleState.PatronSearchResults:
                    _currentState = await PatronSearchResults();
                    break;
                case ConsoleState.PatronDetails:
                    _currentState = await PatronDetails();
                    break;
                case ConsoleState.LoanDetails:
                    _currentState = await LoanDetails();
                    break;
            }
        }
    }

    async Task<ConsoleState> PatronSearch()
    {
        string searchInput = ReadPatronName();

        matchingPatrons = await _patronRepository.SearchPatrons(searchInput);

        // Guard-style clauses for edge cases
        if (matchingPatrons.Count > 20)
        {
            Console.WriteLine("More than 20 patrons satisfy the search, please provide more specific input...");
            return ConsoleState.PatronSearch;
        }
        else if (matchingPatrons.Count == 0)
        {
            Console.WriteLine("No matching patrons found.");
            return ConsoleState.PatronSearch;
        }

        Console.WriteLine("Matching Patrons:");
        PrintPatronsList(matchingPatrons);
        return ConsoleState.PatronSearchResults;
    }

    static string ReadPatronName()
    {
        string? searchInput = null;
        while (String.IsNullOrWhiteSpace(searchInput))
        {
            Console.Write("Enter a string to search for patrons by name: ");

            searchInput = Console.ReadLine();
        }
        return searchInput;
    }

    static void PrintPatronsList(List<Patron> matchingPatrons)
    {
        int patronNumber = 1;
        foreach (Patron patron in matchingPatrons)
        {
            Console.WriteLine($"{patronNumber}) {patron.Name}");
            patronNumber++;
        }
    }

    async Task<ConsoleState> PatronSearchResults()
    {
        CommonActions options = CommonActions.Select | CommonActions.SearchPatrons | CommonActions.Quit;
        CommonActions action = ReadInputOptions(options, out int selectedPatronNumber);
        if (action == CommonActions.Select)
        {
            if (selectedPatronNumber >= 1 && selectedPatronNumber <= matchingPatrons.Count)
            {
                var selectedPatron = matchingPatrons.ElementAt(selectedPatronNumber - 1);
                var patron = await _patronRepository.GetPatron(selectedPatron.Id);
                if (patron == null)
                {
                    Console.WriteLine("選択したパトロンの詳細が見つかりませんでした。再検索してください。");
                    return ConsoleState.PatronSearchResults;
                }
                selectedPatronDetails = patron;
                return ConsoleState.PatronDetails;
            }
            else
            {
                Console.WriteLine("Invalid patron number. Please try again.");
                return ConsoleState.PatronSearchResults;
            }
        }
        else if (action == CommonActions.Quit)
        {
            return ConsoleState.Quit;
        }
        else if (action == CommonActions.SearchPatrons)
        {
            return ConsoleState.PatronSearch;
        }

        throw new InvalidOperationException("An input option is not handled.");
    }

    static CommonActions ReadInputOptions(CommonActions options, out int optionNumber)
    {
        CommonActions action;
        optionNumber = 0;
        do
        {
            Console.WriteLine();
            WriteInputOptions(options);
            string? userInput = Console.ReadLine();

            action = userInput switch
            {
                "q" when options.HasFlag(CommonActions.Quit) => CommonActions.Quit,
                "s" when options.HasFlag(CommonActions.SearchPatrons) => CommonActions.SearchPatrons,
                "b" when options.HasFlag(CommonActions.SearchBooks) => CommonActions.SearchBooks,
                "m" when options.HasFlag(CommonActions.RenewPatronMembership) => CommonActions.RenewPatronMembership,
                "e" when options.HasFlag(CommonActions.ExtendLoanedBook) => CommonActions.ExtendLoanedBook,
                "r" when options.HasFlag(CommonActions.ReturnLoanedBook) => CommonActions.ReturnLoanedBook,
                _ when int.TryParse(userInput, out optionNumber) => CommonActions.Select,
                _ => CommonActions.Repeat
            };

            if (action == CommonActions.Repeat)
            {
                Console.WriteLine("Invalid input. Please try again.");
            }
        } while (action == CommonActions.Repeat);
        return action;
    }

    static void WriteInputOptions(CommonActions options)
    {
        Console.WriteLine("Input Options:");
        if (options.HasFlag(CommonActions.ReturnLoanedBook))
        {
            Console.WriteLine(" - \"r\" to mark as returned");
        }
        if (options.HasFlag(CommonActions.ExtendLoanedBook))
        {
            Console.WriteLine(" - \"e\" to extend the book loan");
        }
        if (options.HasFlag(CommonActions.RenewPatronMembership))
        {
            Console.WriteLine(" - \"m\" to extend patron's membership");
        }
        if (options.HasFlag(CommonActions.SearchPatrons))
        {
            Console.WriteLine(" - \"s\" for new search");
        }
        if (options.HasFlag(CommonActions.Quit))
        {
            Console.WriteLine(" - \"q\" to quit");
        }
        if (options.HasFlag(CommonActions.Select))
        {
            Console.WriteLine("Or type a number to select a list item.");
        }
        if (options.HasFlag(CommonActions.SearchBooks))
        {
            Console.WriteLine(" - \"b\" to check for book availability");
        }
    }

    async Task<ConsoleState> PatronDetails()
    {
        if (selectedPatronDetails == null)
        {
            Console.WriteLine("パトロンが選択されていません。検索に戻ります。");
            return ConsoleState.PatronSearch;
        }
        Console.WriteLine($"Name: {selectedPatronDetails.Name}");
        Console.WriteLine($"Membership Expiration: {selectedPatronDetails.MembershipEnd}");
        Console.WriteLine();
        Console.WriteLine("Book Loans:");
        int loanNumber = 1;
        foreach (Loan loan in selectedPatronDetails.Loans)
        {
            Console.WriteLine($"{loanNumber}) {loan.BookItem!.Book!.Title} - Due: {loan.DueDate} - Returned: {(loan.ReturnDate != null).ToString()}");
            loanNumber++;
        }

        CommonActions options = CommonActions.SearchPatrons | CommonActions.Quit | CommonActions.Select | CommonActions.RenewPatronMembership | CommonActions.SearchBooks;
        CommonActions action = ReadInputOptions(options, out int selectedLoanNumber);
        if (action == CommonActions.Select)
        {
            if (selectedLoanNumber >= 1 && selectedLoanNumber <= selectedPatronDetails.Loans.Count())
            {
                var selectedLoan = selectedPatronDetails.Loans.ElementAt(selectedLoanNumber - 1);
                selectedLoanDetails = selectedPatronDetails.Loans.Where(l => l.Id == selectedLoan.Id).Single();
                return ConsoleState.LoanDetails;
            }
            else
            {
                Console.WriteLine("Invalid book loan number. Please try again.");
                return ConsoleState.PatronDetails;
            }
        }
        else if (action == CommonActions.Quit)
        {
            return ConsoleState.Quit;
        }
        else if (action == CommonActions.SearchPatrons)
        {
            return ConsoleState.PatronSearch;
        }
        else if (action == CommonActions.RenewPatronMembership)
        {
            var status = await _patronService.RenewMembership(selectedPatronDetails.Id);
            Console.WriteLine(EnumHelper.GetDescription(status));
            // reloading after renewing membership
            var reloaded = await _patronRepository.GetPatron(selectedPatronDetails.Id);
            if (reloaded == null)
            {
                Console.WriteLine("更新後のパトロン情報を読み込めませんでした。検索に戻ります。");
                return ConsoleState.PatronSearch;
            }
            selectedPatronDetails = reloaded;
            return ConsoleState.PatronDetails;
        }

        else if (action == CommonActions.SearchBooks)
        {
            return await SearchBooks();
        }

        throw new InvalidOperationException("An input option is not handled.");
    }

    async Task<ConsoleState> LoanDetails()
    {
        if (selectedPatronDetails == null || selectedLoanDetails == null)
        {
            Console.WriteLine("ローンまたはパトロンが選択されていません。検索に戻ります。");
            return ConsoleState.PatronSearch;
        }
        Console.WriteLine($"Book title: {selectedLoanDetails.BookItem!.Book!.Title}");
        Console.WriteLine($"Book Author: {selectedLoanDetails.BookItem!.Book!.Author!.Name}");
        Console.WriteLine($"Due date: {selectedLoanDetails.DueDate}");
        Console.WriteLine($"Returned: {(selectedLoanDetails.ReturnDate != null).ToString()}");
        Console.WriteLine();

        CommonActions options = CommonActions.SearchPatrons | CommonActions.Quit | CommonActions.ReturnLoanedBook | CommonActions.ExtendLoanedBook;
        CommonActions action = ReadInputOptions(options, out int selectedLoanNumber);

        if (action == CommonActions.ExtendLoanedBook)
        {
            var status = await _loanService.ExtendLoan(selectedLoanDetails.Id);
            Console.WriteLine(EnumHelper.GetDescription(status));

            // reload loan after extending
            var reloadedPatron = await _patronRepository.GetPatron(selectedPatronDetails.Id);
            if (reloadedPatron == null)
            {
                Console.WriteLine("パトロン情報の再読み込みに失敗しました。");
                return ConsoleState.PatronSearch;
            }
            selectedPatronDetails = reloadedPatron;

            var reloadedLoan = await _loanRepository.GetLoan(selectedLoanDetails.Id);
            if (reloadedLoan == null)
            {
                Console.WriteLine("ローン情報の再読み込みに失敗しました。");
                return ConsoleState.PatronDetails;
            }
            selectedLoanDetails = reloadedLoan;
            return ConsoleState.LoanDetails;
        }
        else if (action == CommonActions.ReturnLoanedBook)
        {
            var status = await _loanService.ReturnLoan(selectedLoanDetails.Id);

            Console.WriteLine(EnumHelper.GetDescription(status));
            _currentState = ConsoleState.LoanDetails;
            // reload loan after returning
            var reloadedLoan = await _loanRepository.GetLoan(selectedLoanDetails.Id);
            if (reloadedLoan == null)
            {
                Console.WriteLine("ローン情報の再読み込みに失敗しました。");
                return ConsoleState.PatronDetails;
            }
            selectedLoanDetails = reloadedLoan;
            return ConsoleState.LoanDetails;
        }
        else if (action == CommonActions.Quit)
        {
            return ConsoleState.Quit;
        }
        else if (action == CommonActions.SearchPatrons)
        {
            return ConsoleState.PatronSearch;
        }

        throw new InvalidOperationException("An input option is not handled.");
    }

    async Task<ConsoleState> SearchBooks()
    {
        Console.Write("検索する本のタイトルを入力してください: ");
        string? searchInput = Console.ReadLine();
        if (String.IsNullOrWhiteSpace(searchInput))
        {
            Console.WriteLine("検索文字列が入力されませんでした。");
            return ConsoleState.PatronDetails;
        }

        await _jsonData.EnsureDataLoaded();

        var book = _jsonData.Books?.FirstOrDefault(b => !string.IsNullOrEmpty(b.Title) && b.Title.Contains(searchInput, StringComparison.OrdinalIgnoreCase));
        if (book == null)
        {
            Console.WriteLine("該当する書籍が見つかりませんでした。");
            return ConsoleState.PatronDetails;
        }

        // Find all book items (copies) for this book
        var bookItems = _jsonData.BookItems?.Where(bi => bi.BookId == book.Id).ToList();
        if (bookItems == null || bookItems.Count == 0)
        {
            Console.WriteLine($"{book.Title} の蔵書が見つかりませんでした。");
            return ConsoleState.PatronDetails;
        }

        // If any copy is not currently loaned (no active loan), report available
        foreach (var bi in bookItems)
        {
            var activeLoan = _jsonData.Loans?.FirstOrDefault(l => l.BookItemId == bi.Id && l.ReturnDate == null);
            if (activeLoan == null)
            {
                Console.WriteLine($"{book.Title} 貸し出可能");
                return ConsoleState.PatronDetails;
            }
        }

        // All copies are loaned out; show due date of first active loan
        var firstActiveLoan = _jsonData.Loans!.FirstOrDefault(l => bookItems.Select(b => b.Id).Contains(l.BookItemId) && l.ReturnDate == null);
        if (firstActiveLoan != null)
        {
            Console.WriteLine($"{book.Title}は別のパトロンに貸し出されています。申告期限は{firstActiveLoan.DueDate:yyyy-MM-dd}です。");
            return ConsoleState.PatronDetails;
        }

        // Fallback
        Console.WriteLine($"{book.Title} の貸し出状況を確認できませんでした。");
        return ConsoleState.PatronDetails;
    }
}
