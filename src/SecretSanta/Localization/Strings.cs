namespace SecretSanta.Localization;

public static class Strings
{
    // Each entry: ru, en, hy.
    public static readonly IReadOnlyDictionary<string, string[]> All = new Dictionary<string, string[]>
    {
        ["Tagline"] = ["Тайный Санта для своих", "Secret Santa for your team", "Գաղտնի Ձմեռ պապ ձեր թիմի համար"],
        ["Footer"] = ["Пары знает только сервер.", "Only the server knows the pairs.", "Զույգերը գիտի միայն սերվերը։"],

        ["NewGame"] = ["Новая игра", "New game", "Նոր խաղ"],
        ["HowItWorks"] = ["Как это работает", "How it works", "Ինչպես է սա աշխատում"],
        ["Step1"] = ["Создайте игру и разошлите коллегам ссылку-приглашение.", "Create a game and share the invite link with your colleagues.", "Ստեղծեք խաղ և ուղարկեք հրավերի հղումը գործընկերներին։"],
        ["Step2"] = ["Каждый вписывает имя и вишлист и получает свою личную ссылку.", "Everyone enters a name and a wishlist and gets a personal link.", "Յուրաքանչյուրը գրում է իր անունն ու ցանկությունների ցուցակը և ստանում անձնական հղում։"],
        ["Step3"] = ["Вы проводите жеребьёвку, и каждый по своей ссылке видит, кому дарит.", "You run the draw, and everyone sees who they give to via their link.", "Դուք անցկացնում եք վիճակահանությունը, և յուրաքանչյուրն իր հղումով տեսնում է, թե ում է նվեր տալիս։"],
        ["Budget"] = ["Бюджет подарка", "Gift budget", "Նվերի բյուջե"],
        ["Note"] = ["Комментарий для участников", "Note for participants", "Նշում մասնակիցների համար"],
        ["NotePlaceholder"] = ["Где и когда дарим, правила…", "Where and when, any rules…", "Որտեղ և երբ, կանոններ…"],
        ["Create"] = ["Создать игру", "Create game", "Ստեղծել խաղը"],
        ["Required"] = ["Заполните это поле.", "Please fill in this field.", "Լրացրեք այս դաշտը։"],

        ["AdminTitle"] = ["Пульт организатора", "Organizer panel", "Կազմակերպչի վահանակ"],
        ["AdminSaveLink"] = ["Сохраните адрес этой страницы в закладки: это ваш единственный доступ к управлению игрой.", "Bookmark this page: it is your only way to manage the game.", "Պահպանեք այս էջի հասցեն էջանիշներում․ սա խաղը կառավարելու ձեր միակ միջոցն է։"],
        ["InviteLink"] = ["Ссылка-приглашение", "Invite link", "Հրավերի հղում"],
        ["InviteHint"] = ["Отправьте её коллегам. Если вы тоже участвуете, зарегистрируйтесь по ней сами.", "Send it to your colleagues. If you are playing too, join through it yourself.", "Ուղարկեք այն գործընկերներին։ Եթե դուք էլ եք մասնակցում, գրանցվեք նույն հղումով։"],
        ["Copy"] = ["Копировать", "Copy", "Պատճենել"],
        ["Copied"] = ["Скопировано", "Copied", "Պատճենված է"],
        ["Participants"] = ["Участники", "Participants", "Մասնակիցներ"],
        ["NoParticipants"] = ["Пока никого. Разошлите приглашение.", "Nobody yet. Share the invite link.", "Դեռ ոչ ոք չկա։ Ուղարկեք հրավերը։"],
        ["WishlistFilled"] = ["вишлист заполнен", "wishlist filled in", "ցուցակը լրացված է"],
        ["WishlistEmpty"] = ["вишлист пуст", "no wishlist yet", "ցուցակը դատարկ է"],
        ["Remove"] = ["Удалить", "Remove", "Հեռացնել"],
        ["RemoveConfirm"] = ["Удалить участника «{0}»?", "Remove \"{0}\" from the game?", "Հեռացնե՞լ «{0}» մասնակցին։"],
        ["NewLink"] = ["Новая ссылка", "New link", "Նոր հղում"],
        ["NewLinkConfirm"] = ["Выдать участнику «{0}» новую личную ссылку? Старая перестанет работать. Не открывайте новую сами: там видно, кому участник дарит подарок.", "Issue a new personal link for \"{0}\"? The old one will stop working. Don't open the new one yourself: it shows who they give to.", "Նոր անձնական հղում տրամադրե՞լ «{0}» մասնակցին։ Հինն այլևս չի աշխատի։ Նորը ինքներդ մի՛ բացեք․ այնտեղ երևում է, թե ում է նա նվեր տալիս։"],
        ["NewLinkIssued"] = ["Новая личная ссылка для «{0}». Передайте её лично и не открывайте сами.", "New personal link for \"{0}\". Pass it on privately and don't open it yourself.", "Նոր անձնական հղում «{0}» մասնակցի համար։ Փոխանցեք այն անձամբ և ինքներդ մի՛ բացեք։"],
        ["Exclusions"] = ["Исключения", "Exclusions", "Բացառություններ"],
        ["ExclusionsHint"] = ["Эти двое не будут дарить друг другу: например, пары или соседи по кабинету.", "These two won't give to each other: e.g. couples or office neighbours.", "Այս երկուսը միմյանց նվեր չեն տա․ օրինակ՝ զույգերը կամ սենյակակիցները։"],
        ["AddExclusion"] = ["Добавить", "Add", "Ավելացնել"],
        ["NoExclusions"] = ["Исключений нет.", "No exclusions.", "Բացառություններ չկան։"],
        ["ExclusionsNeedTwo"] = ["Исключения можно задать, когда участников хотя бы двое.", "You can add exclusions once there are at least two participants.", "Բացառություններ կարելի է ավելացնել, երբ կա առնվազն երկու մասնակից։"],
        ["Draw"] = ["Провести жеребьёвку", "Run the draw", "Անցկացնել վիճակահանությունը"],
        ["DrawConfirm"] = ["Провести жеребьёвку? После неё новые участники не смогут присоединиться.", "Run the draw? New participants won't be able to join afterwards.", "Անցկացնե՞լ վիճակահանությունը։ Դրանից հետո նոր մասնակիցներ չեն կարողանա միանալ։"],
        ["DrawNeedMore"] = ["Для жеребьёвки нужно минимум 3 участника.", "The draw needs at least 3 participants.", "Վիճակահանության համար անհրաժեշտ է առնվազն 3 մասնակից։"],
        ["DrawImpossible"] = ["С такими исключениями пары не складываются. Уберите часть исключений.", "No valid pairing exists with these exclusions. Remove some of them.", "Այս բացառություններով զույգեր կազմել հնարավոր չէ։ Հեռացրեք դրանցից մի քանիսը։"],
        ["DrawDone"] = ["Жеребьёвка проведена {0}. Каждый видит своего получателя по личной ссылке.", "The draw took place on {0}. Everyone sees their recipient via their personal link.", "Վիճակահանությունն անցկացվել է {0}։ Յուրաքանչյուրն իր ստացողին տեսնում է անձնական հղումով։"],
        ["Undraw"] = ["Отменить жеребьёвку", "Cancel the draw", "Չեղարկել վիճակահանությունը"],
        ["UndrawConfirm"] = ["Отменить жеребьёвку? Пары сбросятся, и присоединиться снова будет можно.", "Cancel the draw? All pairs are reset and joining opens again.", "Չեղարկե՞լ վիճակահանությունը։ Զույգերը կչեղարկվեն, և կրկին հնարավոր կլինի միանալ։"],
        ["OrganizerBlind"] = ["Пары не видны даже вам: сюрприз для всех.", "Even you can't see the pairs: it's a surprise for everyone.", "Զույգերը տեսանելի չեն նույնիսկ ձեզ․ անակնկալ է բոլորի համար։"],
        ["LockedAfterDraw"] = ["После жеребьёвки состав и исключения менять нельзя. Сначала отмените жеребьёвку.", "After the draw, participants and exclusions are locked. Cancel the draw first.", "Վիճակահանությունից հետո մասնակիցներին և բացառությունները փոխել հնարավոր չէ։ Նախ չեղարկեք վիճակահանությունը։"],

        ["JoinTitle"] = ["Присоединиться к игре", "Join the game", "Միանալ խաղին"],
        ["YourName"] = ["Имя и фамилия", "Full name", "Անուն և ազգանուն"],
        ["YourNameHint"] = ["Так вас увидит тот, кто будет вам дарить.", "This is how your Secret Santa will see you.", "Այսպես ձեզ կտեսնի նա, ով ձեզ նվեր է տալու։"],
        ["Wishlist"] = ["Вишлист", "Wishlist", "Ցանկությունների ցուցակ"],
        ["WishlistHint"] = ["Чему вы обрадуетесь: идеи, ссылки, размеры.", "What would make you happy: ideas, links, sizes.", "Ինչն է ձեզ ուրախացնելու՝ գաղափարներ, հղումներ, չափսեր։"],
        ["NotWanted"] = ["Что не дарить", "Please don't give", "Ինչ չնվիրել"],
        ["NotWantedHint"] = ["Аллергии, «ещё одна кружка», всё, что точно мимо.", "Allergies, \"yet another mug\", anything that would miss.", "Ալերգիաներ, «ևս մեկ բաժակ», այն ամենը, ինչ հաստատ պետք չէ։"],
        ["JoinButton"] = ["Участвую!", "I'm in!", "Մասնակցում եմ"],
        ["JoinClosed"] = ["Жеребьёвка уже проведена, присоединиться нельзя. Напишите организатору.", "The draw has already taken place, so joining is closed. Contact the organizer.", "Վիճակահանությունն արդեն անցկացվել է, միանալ հնարավոր չէ։ Դիմեք կազմակերպչին։"],
        ["AlreadyJoined"] = ["Вы уже участвуете в этой игре как «{0}».", "You are already in this game as \"{0}\".", "Դուք արդեն մասնակցում եք այս խաղին որպես «{0}»։"],
        ["JoinAnother"] = ["Зарегистрировать другого участника", "Register someone else", "Գրանցել այլ մասնակցի"],
        ["OpenMyPage"] = ["Открыть мою страницу", "Open my page", "Բացել իմ էջը"],
        ["NameTaken"] = ["Такое имя уже есть. Добавьте фамилию или инициал.", "That name is already taken. Add a surname or an initial.", "Այս անունն արդեն կա։ Ավելացրեք ազգանունը կամ սկզբնատառը։"],
        ["JoinedCount"] = ["Уже участвуют: {0}", "Joined so far: {0}", "Արդեն մասնակցում են՝ {0}"],

        ["SaveYourLink"] = ["Это ваша личная ссылка. Сохраните её: по ней вы узнаете, кому дарите подарок.", "This is your personal link. Save it: this is where you'll find out who you give to.", "Սա ձեր անձնական հղումն է։ Պահպանեք այն․ դրանով կիմանաք, թե ում եք նվեր տալու։"],
        ["DontShare"] = ["Никому её не пересылайте.", "Don't share it with anyone.", "Այն ոչ մեկի հետ մի՛ կիսվեք։"],
        ["Hello"] = ["Привет, {0}!", "Hi, {0}!", "Բարև, {0}"],
        ["NotDrawnYet"] = ["Жеребьёвка ещё не проведена. Загляните позже: здесь появится имя вашего получателя.", "The draw hasn't taken place yet. Check back later: your recipient's name will appear here.", "Վիճակահանությունը դեռ չի անցկացվել։ Ավելի ուշ նայեք․ այստեղ կհայտնվի ձեր ստացողի անունը։"],
        ["YouGiveTo"] = ["Вы дарите подарок", "You are giving a gift to", "Դուք նվեր եք տալիս"],
        ["Reveal"] = ["Открыть конверт", "Open the envelope", "Բացել ծրարը"],
        ["TheirWishlist"] = ["Вишлист получателя", "Their wishlist", "Ստացողի ցանկությունները"],
        ["TheirNotWanted"] = ["Не дарить", "Please don't give", "Չնվիրել"],
        ["EmptyWishlist"] = ["Пусто: придётся проявить фантазию.", "Empty: time to get creative.", "Դատարկ է․ ստիպված կլինեք երևակայել։"],
        ["MyWishlist"] = ["Мой вишлист", "My wishlist", "Իմ ցանկությունները"],
        ["MyWishlistHint"] = ["Его можно менять в любой момент, даритель увидит изменения.", "You can change it any time; your Secret Santa sees the updates.", "Կարող եք փոխել ցանկացած պահի, նվիրողը կտեսնի փոփոխությունները։"],
        ["Save"] = ["Сохранить", "Save", "Պահպանել"],
        ["Saved"] = ["Сохранено.", "Saved.", "Պահպանված է։"],
        ["BudgetIs"] = ["Бюджет {0}", "Budget {0}", "Բյուջե՝ {0}"],

        ["PairsTitle"] = ["Кто кому дарит", "Who gives to whom", "Ով ում է նվիրում"],
        ["PairsNotDrawn"] = ["Жеребьёвка ещё не проведена.", "The draw hasn't taken place yet.", "Վիճակահանությունը դեռ չի անցկացվել։"],

        ["NotFoundTitle"] = ["Ссылка не найдена", "Link not found", "Հղումը չի գտնվել"],
        ["NotFoundText"] = ["Возможно, она устарела. Спросите организатора.", "It may be outdated. Ask the organizer.", "Հնարավոր է՝ այն հնացել է։ Դիմեք կազմակերպչին։"],
        ["ErrorTitle"] = ["Что-то пошло не так", "Something went wrong", "Ինչ-որ բան այն չէ"],
        ["ErrorText"] = ["Попробуйте ещё раз чуть позже.", "Please try again a bit later.", "Փորձեք կրկին մի փոքր ուշ։"],
        ["BackHome"] = ["На главную", "Home", "Գլխավոր էջ"],
    };

    public static string Get(string key, string lang)
    {
        if (!All.TryGetValue(key, out var values)) return key;
        var index = Array.IndexOf(Languages.All, lang);
        return values[index < 0 ? 0 : index];
    }
}
