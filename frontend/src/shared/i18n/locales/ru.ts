// ru — Russian.
//
// Edit: the vocabulary is what a Russian дилерский центр uses, not a dictionary
//       rendering of the English. A lead is an *обращение* (the CRM word), the
//       service department is *сервис* rather than "мастерская", and vehicle
//       stock is *склад* — which means warehouse elsewhere but is the trade word
//       for cars on the lot. Accounting follows РСБУ usage:
//       *оборотно-сальдовая ведомость* is the trial balance.
//
//       Formal *вы*, lower case, as Russian business software writes it.
//
//       Russian has FOUR plural categories and they are not optional. `2 сделки`
//       and `5 сделок` differ, and so do `21 сделка` and `22 сделки`. Every
//       counted noun in this file is a plural entry, chosen by Intl.PluralRules.

import type { Catalogue } from '../index';

export const ru: Catalogue = {
  'app.name': 'DealerFOSS',
  'common.loading': 'Загрузка…',
  'common.save': 'Сохранить',
  'common.saving': 'Сохранение…',
  'common.cancel': 'Отмена',
  'common.close': 'Закрыть',
  'common.retry': 'Повторить',
  'common.search': 'Поиск',
  'common.searching': 'Поиск…',
  'common.none': 'Нет',
  'common.all': 'Все',
  'common.yes': 'Да',
  'common.no': 'Нет',
  'common.back': 'Назад',
  'common.continue': 'Продолжить',
  'common.unexpected': 'Что-то пошло не так. Попробуйте ещё раз.',
  'common.notPermitted': 'У вас нет прав на просмотр этого раздела.',
  'common.unreachable': 'Не удалось связаться с сервером. Он запущен?',

  'error.network': 'Не удалось связаться с сервером. Он запущен?',
  'error.invalidCredentials': 'Эта почта и пароль не подходят ни к одной учётной записи.',
  'error.sessionRequired': 'Войдите, чтобы воспользоваться этим.',
  'error.adminSessionRequired':
    'Войдите как администратор, чтобы открыть консоль управления. Вход сотрудника дилерского центра туда не ведёт.',
  'error.sessionInvalid': 'Ваш сеанс завершён. Войдите снова.',
  'error.antiForgeryFailed':
    'У этого браузера больше нет токена, подходящего к его сеансу. Войдите снова.',
  'error.secondFactorRejected': 'Этот код не принят.',
  'error.secondFactorRequired':
    'Ваша роль требует двухэтапного входа. Настройте его, чтобы открыть остальную часть приложения.',
  'error.mfaNotEnrolled': 'Для этой учётной записи двухэтапный вход не настроен.',
  'error.mfaAlreadyOn': 'Для этой учётной записи двухэтапный вход уже включён.',
  'error.notATenantCaller':
    'Администратор не может действовать от имени сотрудника дилерского центра.',
  'error.tenantRequired': 'Укажите, о какой дилерской группе идёт речь.',
  'error.tenantNotFound': 'Активная дилерская группа с таким названием не найдена.',

  'shell.skipToContent': 'Перейти к содержимому',
  'shell.mainNavigation': 'Основное меню',
  'shell.signOut': 'Выйти',
  'shell.shortcuts': 'Горячие клавиши',
  'shell.shortcutsTitle': 'Горячие клавиши ( ? )',
  'shell.language': 'Язык',
  'shell.appearance': 'Оформление',

  'nav.dashboard': 'Этот месяц',
  'nav.customers': 'Клиенты',
  'nav.leads': 'Обращения',
  'nav.deals': 'Сделки',
  'nav.stock': 'Склад',
  'nav.workshop': 'Сервис',
  'nav.parts': 'Запчасти',
  'nav.trialBalance': 'Ведомость',
  'nav.books': 'Периоды',
  'nav.records': 'Данные',
  'nav.staff': 'Сотрудники',
  'nav.secondFactor': 'Двухэтапный вход',

  'appearance.auto': 'Авто',
  'appearance.autoHint': 'Как на устройстве',
  'appearance.light': 'Светлая',
  'appearance.lightHint': 'Всегда светлая',
  'appearance.dark': 'Тёмная',
  'appearance.darkHint': 'Всегда тёмная',

  'signIn.lede': 'Войдите в свой дилерский центр.',
  'signIn.dealerGroup': 'Дилерская группа',
  'signIn.email': 'Электронная почта',
  'signIn.password': 'Пароль',
  'signIn.submit': 'Войти',
  'signIn.submitting': 'Вход…',
  'signIn.codeLede':
    'Введите шестизначный код из приложения-аутентификатора или один из резервных кодов.',
  'signIn.code': 'Код',
  'signIn.checking': 'Проверка…',
  'signIn.startAgain': 'Начать заново',

  'setPassword.title': 'Задайте пароль',
  'setPassword.lede':
    'Руководитель выдал вам код. Используйте его здесь один раз, чтобы выбрать пароль, который знаете только вы, — никто в дилерском центре не увидит, что вы выбрали.',
  'setPassword.dealership': 'Дилерский центр',
  'setPassword.email': 'Электронная почта',
  'setPassword.code': 'Код',
  'setPassword.password': 'Новый пароль',
  'setPassword.passwordHint':
    'Не менее 12 символов. Именно длина делает пароль трудным для подбора.',
  'setPassword.again': 'Повторите новый пароль',
  'setPassword.mismatch': 'Эти два пароля не совпадают.',
  'setPassword.failed': 'Не получилось.',
  'setPassword.submit': 'Задать пароль',
  'setPassword.doneTitle': 'Всё готово',
  'setPassword.doneLede': 'Войдите, указав адрес почты и только что выбранный пароль.',
  'setPassword.toSignIn': 'Перейти ко входу',

  'secondFactor.title': 'Двухэтапный вход',
  'secondFactor.required':
    'Для вашей роли дилерский центр требует двухэтапный вход. Пока вы его не настроите, это единственный доступный вам экран.',
  'secondFactor.intro':
    'После этого при входе, кроме пароля, будет запрашиваться шестизначный код из приложения на телефоне. Подойдут Google Authenticator, Authy и 1Password.',
  'secondFactor.start': 'Начать',
  'secondFactor.starting': 'Подготовка…',
  'secondFactor.pointApp': 'Наведите приложение-аутентификатор на этот квадрат.',
  'secondFactor.qrTitle': 'Отсканируйте это приложением-аутентификатором',
  'secondFactor.cannotScan': 'Не удаётся отсканировать?',
  'secondFactor.typeInstead': 'Тогда введите это в приложение вручную:',
  'secondFactor.enterCode': 'Теперь введите показанный код',
  'secondFactor.turnOn': 'Включить',
  'secondFactor.checking': 'Проверка…',
  'secondFactor.notYet':
    'Во входе пока ничего не изменилось. Это вступит в силу только после того, как код выше будет принят.',
  'secondFactor.onNow':
    'Двухэтапный вход включён. Теперь после пароля у вас будут спрашивать код.',
  'secondFactor.saveTitle': 'Сохраните их в надёжном месте',
  'secondFactor.saveLede':
    'Каждый работает один раз и только если вы потеряете телефон. Это единственный раз, когда они показываются.',
  'secondFactor.recoveryCodes': 'Резервные коды',

  'shortcuts.title': 'Горячие клавиши',
  'shortcuts.space': 'Пробел',
  'shortcuts.note':
    'Пока вы пишете в поле, горячие клавиши не срабатывают — так они никогда не съедят символ, который вы набирали.',
  'shortcuts.goDashboard': 'К сводке за месяц',
  'shortcuts.goStock': 'К складу',
  'shortcuts.goCustomers': 'К клиентам',
  'shortcuts.goLeads': 'К обращениям',
  'shortcuts.goDeals': 'К сделкам',
  'shortcuts.goWorkshop': 'К сервису',
  'shortcuts.goParts': 'К запчастям',
  'shortcuts.goBooks': 'К учётным периодам',
  'shortcuts.monthBefore': 'В сводке: предыдущий месяц',
  'shortcuts.monthAfter': 'В сводке: следующий месяц',
  'shortcuts.thisMonth': 'В сводке: вернуться к текущему месяцу',
  'shortcuts.showList': 'Показать этот список',

  // Отраслевые слова: «предпродажная подготовка» — это то, что в дилерском
  // центре делают с автомобилем перед продажей, а машина, придержанная под
  // сделку, «забронирована», а не «в ожидании».
  'enum.inventoryStatus.Incoming': 'В пути',
  'enum.inventoryStatus.Reconditioning': 'Предпродажная подготовка',
  'enum.inventoryStatus.Available': 'Доступен',
  'enum.inventoryStatus.OnHold': 'Забронирован',
  'enum.inventoryStatus.Sold': 'Продан',
  'enum.inventoryStatus.Removed': 'Снят',

  'enum.leadStatus.New': 'Новое',
  'enum.leadStatus.Working': 'В работе',
  'enum.leadStatus.Appointment': 'Назначена встреча',
  'enum.leadStatus.Won': 'Успешное',
  'enum.leadStatus.Lost': 'Потеряно',

  'enum.leadSource.WalkIn': 'Визит без записи',
  'enum.leadSource.Phone': 'Телефон',
  'enum.leadSource.Website': 'Сайт',
  'enum.leadSource.Referral': 'Рекомендация',
  'enum.leadSource.Marketplace': 'Площадка объявлений',
  'enum.leadSource.Unknown': 'Не указан',

  'enum.dealStatus.Draft': 'Черновик',
  'enum.dealStatus.Submitted': 'На согласовании',
  'enum.dealStatus.Approved': 'Согласована',
  'enum.dealStatus.Delivered': 'Выдан автомобиль',
  'enum.dealStatus.Lost': 'Потеряна',

  'enum.chargeKind.VehiclePrice': 'Цена автомобиля',
  'enum.chargeKind.Fee': 'Сбор',
  'enum.chargeKind.Discount': 'Скидка',
  'enum.chargeKind.Accessory': 'Аксессуар',

  'enum.repairOrderStatus.Booked': 'Записан',
  'enum.repairOrderStatus.InProgress': 'В работе',
  'enum.repairOrderStatus.Completed': 'Выполнен',
  'enum.repairOrderStatus.Invoiced': 'Выставлен счёт',
  'enum.repairOrderStatus.Cancelled': 'Отменён',

  'enum.serviceLineKind.Labour': 'Работы',
  'enum.serviceLineKind.Part': 'Запчасть',
  'enum.serviceLineKind.Sublet': 'Сторонние работы',

  'enum.financeProductKind.Warranty': 'Гарантия',
  'enum.financeProductKind.Gap': 'GAP',
  'enum.financeProductKind.ServicePlan': 'Сервисный контракт',
  'enum.financeProductKind.Protection': 'Защитное покрытие',
  'enum.financeProductKind.Other': 'Прочее',

  'enum.periodState.Open': 'Открыт',
  'enum.periodState.Closed': 'Закрыт',

  'enum.booksState.NotOpened': 'Не открыт',
  'enum.booksState.Open': 'Открыт',
  'enum.booksState.Closed': 'Закрыт',
  'enum.booksState.Unknown': 'Неизвестно',

  'enum.importKind.Customers': 'Клиенты',
  'enum.importKind.Vehicles': 'Автомобили',

  'enum.tenantStatus.Active': 'Активен',
  'enum.tenantStatus.Suspended': 'Приостановлен',
  'enum.tenantStatus.Provisioning': 'Создаётся',
  'enum.tenantStatus.Archived': 'В архиве',

  'stock.title': 'Автомобили в наличии',
  'stock.status': 'Статус',
  'stock.loading': 'Загрузка списка автомобилей…',
  'stock.denied':
    'У вас нет доступа к складу этой площадки. Если это ошибка, обратитесь к руководителю.',
  'stock.failed': 'Не удалось загрузить список автомобилей.',
  'stock.empty': 'Пока пусто. Автомобили появляются здесь после постановки на склад.',
  'stock.onlyStockNumber': 'Показан только складской номер {stock}.',
  'stock.showEverything': 'Показать всё',
  'stock.colStock': 'Складской №',
  'stock.colVehicle': 'Автомобиль',
  'stock.colVin': 'VIN',
  'stock.colStatus': 'Статус',
  // Четыре формы: 1 машина, 3 машины, 11 машин, 22 машины.
  'stock.count': {
    one: '{count} машина в наличии',
    few: '{count} машины в наличии',
    many: '{count} машин в наличии',
    other: '{count} машины в наличии',
  },
  'stock.countCapped': 'Первые {count} машин в наличии. Возможно, есть ещё.',
  'stock.cappedNote':
    'Показаны первые {count}. Возможно, есть ещё — сузьте выборку фильтром по статусу, пока нет постраничного вывода.',

  'enum.accountKind.Asset': 'Актив',
  'enum.accountKind.Liability': 'Обязательство',
  'enum.accountKind.Equity': 'Капитал',
  'enum.accountKind.Revenue': 'Доходы',
  'enum.accountKind.Expense': 'Расходы',

  'trialBalance.title': 'Оборотно-сальдовая ведомость',
  'trialBalance.loading': 'Подсчёт…',
  'trialBalance.denied': 'У вас нет доступа к этим цифрам.',
  'trialBalance.failed': 'Не удалось загрузить сальдо.',
  'trialBalance.empty':
    'Проводок пока нет. Записи появятся здесь после выдачи первого автомобиля.',
  'trialBalance.inBalance': 'Сходится — дебет и кредит равны {total}.',
  'trialBalance.outOfBalance': 'Расхождение {difference}. Что-то потерялось при вводе.',
  'trialBalance.colCode': 'Номер счёта',
  'trialBalance.colAccount': 'Счёт',
  'trialBalance.colKind': 'Вид',
  'trialBalance.colDebits': 'Дебет',
  'trialBalance.colCredits': 'Кредит',
  'trialBalance.colBalance': 'Сальдо',
  'trialBalance.total': 'Итого',

  'enum.customerKind.Person': 'Физлицо',
  'enum.customerKind.Business': 'Компания',

  'customers.title': 'Клиенты',
  'customers.find': 'Найти человека',
  'customers.findPlaceholder': 'Имя, телефон или почта',
  'customers.add': 'Добавить клиента',
  'customers.looking': 'Идёт поиск…',
  'customers.denied':
    'У вас нет доступа к карточкам клиентов. Если это ошибка, обратитесь к руководителю.',
  'customers.failed': 'Не удалось загрузить клиентов.',
  'customers.noMatches': 'Никто не найден.',
  'customers.colName': 'Имя',
  'customers.colKind': 'Тип',
  'customers.colEmail': 'Почта',
  'customers.colPhone': 'Телефон',
  'customers.count': {
    one: '{count} клиент',
    few: '{count} клиента',
    many: '{count} клиентов',
    other: '{count} клиента',
  },
  'customers.countCapped': 'Первые {count} клиентов. Возможно, есть ещё.',
  'customers.cappedNote':
    'Показаны первые {count}. Возможно, есть ещё — уточните запрос, пока нет постраничного вывода.',

  'customers.kindLabel': 'Физлицо или компания',
  'customers.firstName': 'Имя',
  'customers.lastName': 'Фамилия',
  'customers.businessName': 'Название компании',
  'customers.email': 'Почта',
  'customers.phone': 'Телефон',
  'customers.submit': 'Добавить',
  'customers.checking': 'Проверка на дубликаты…',
  'customers.adding': 'Добавление…',

  'customers.duplicateTitle': 'Похожий человек уже есть',
  'customers.duplicateLede':
    'Вторая карточка на того же человека разрывает его историю — обслуживание, сделки и контакты перестают сходиться. Проверьте, нет ли его среди этих.',
  'customers.noContactDetails': 'контактов нет',
  'customers.oneOfTheseIsThem': 'Это кто-то из них',
  'customers.addAnyway': 'Никто из них — всё равно добавить',

  'enum.importOutcome.Pending': 'В очереди',
  'enum.importOutcome.Created': 'Добавлена',
  'enum.importOutcome.Updated': 'Уже была',
  'enum.importOutcome.Skipped': 'Пропущена',
  'enum.importOutcome.Failed': 'Отклонена',

  'records.title': 'Данные',
  'records.bringIn': 'Загрузить данные',
  'records.bringInLede':
    'Таблица, выгруженная из прежней системы и сохранённая в CSV. Ничего не записывается, пока вы не выполните пробный прогон.',
  'records.whatIsInIt': 'Что в файле',
  'records.file': 'Файл',
  'records.unreadableFile': 'Не удалось прочитать файл. Это текстовый CSV?',
  'records.practice': 'Пробный прогон',
  'records.practising': 'Идёт проверка…',
  'records.importForReal': 'Загрузить по-настоящему',
  'records.importing': 'Загрузка…',
  'records.practiseFirst':
    'Сначала выполните пробный прогон. Он ничего не меняет и показывает, что именно сделает настоящая загрузка.',

  'records.takeOut': 'Выгрузить данные',
  'records.takeOutLede':
    'Скачивает все записи этого типа в CSV. Это тот же формат, который принимает эта страница, — данные можно перенести куда угодно, в том числе в другую систему.',
  'records.downloadCustomers': 'Скачать клиентов',
  'records.downloadVehicles': 'Скачать автомобили',

  'records.couldNotRun': 'Эту загрузку не удалось выполнить.',
  'records.whatWouldHappen': 'Что произошло бы',
  'records.whatHappened': 'Что произошло',
  'records.summaryPractice': {
    one: 'Из {count} строки: {created} было бы добавлено, {updated} уже есть, {skipped} пропущено, {failed} не прочитано.',
    few: 'Из {count} строк: {created} было бы добавлено, {updated} уже есть, {skipped} пропущено, {failed} не прочитано.',
    many: 'Из {count} строк: {created} было бы добавлено, {updated} уже есть, {skipped} пропущено, {failed} не прочитано.',
    other:
      'Из {count} строк: {created} было бы добавлено, {updated} уже есть, {skipped} пропущено, {failed} не прочитано.',
  },
  'records.summaryReal': {
    one: 'Из {count} строки: {created} добавлено, {updated} уже есть, {skipped} пропущено, {failed} отклонено.',
    few: 'Из {count} строк: {created} добавлено, {updated} уже есть, {skipped} пропущено, {failed} отклонено.',
    many: 'Из {count} строк: {created} добавлено, {updated} уже есть, {skipped} пропущено, {failed} отклонено.',
    other:
      'Из {count} строк: {created} добавлено, {updated} уже есть, {skipped} пропущено, {failed} отклонено.',
  },
  'records.nothingWritten': 'Ничего не записано. Это был пробный прогон.',
  'records.rowsToLookAt': 'Строки, которые стоит посмотреть',
  'records.rowsToLookAtLede':
    'Номер строки — тот, который вы видите в своей таблице, а сама строка приведена ровно так, как пришла. Исправьте файл и запустите снова — здесь ничего не редактируется за вас.',
  'records.problemCount': {
    one: '{count} строка требует внимания',
    few: '{count} строки требуют внимания',
    many: '{count} строк требуют внимания',
    other: '{count} строки требуют внимания',
  },
  'records.colLine': 'Строка',
  'records.colWhatHappened': 'Что произошло',
  'records.colTheRow': 'Содержимое строки',
  'records.timeout':
    'Загрузка идёт дольше обычного. Она продолжается — просто эта страница перестала ждать.',

  'periods.title': 'Учётные периоды',
  'periods.loading': 'Загрузка периодов…',
  'periods.denied': 'У вас нет доступа к бухгалтерии.',
  'periods.failed': 'Не удалось прочитать учётные периоды.',
  'periods.actionFailed': 'Не получилось.',
  'periods.lede':
    'Ничего нельзя провести в месяц, пока он не открыт, и ничего нельзя провести в уже закрытый. Закрытие — это действие, которое вы выполняете по завершении работ по закрытию месяца; никакая дата не сделает этого за вас.',
  'periods.none': 'Ни один месяц ещё не открыт. Пока вы не откроете, провести ничего нельзя.',

  'periods.openAMonth': 'Открыть месяц',
  'periods.openLede':
    'Пока месяц не открыт, ничего с датой внутри него провести нельзя — продажа или счёт сервиса будут отклонены. Открытие делается осознанно, чтобы у учёта было начало, выбранное вами, а не выведенное из первой попавшейся записи.',
  'periods.year': 'Год',
  'periods.month': 'Месяц',
  'periods.openIt': 'Открыть',

  'periods.reopenTitle': 'Открыть {month} заново?',
  'periods.reopenLede':
    'Этот месяц закрыт, и его цифры, возможно, уже переданы в отчётность. Повторное открытие фиксируется вместе с вашей причиной, чтобы позже любой мог увидеть, что произошло и почему.',
  'periods.reopenWhy': 'Почему его открывают заново?',
  'periods.reopenPlaceholder': 'Счёт от поставщика пришёл 4-го числа',
  'periods.reopenIt': 'Открыть заново',
  'periods.leaveClosed': 'Оставить закрытым',
  'periods.reopen': 'Открыть заново',

  'periods.closeIt': 'Закрыть',
  'periods.confirmClose':
    'Закрыть {month}? Больше ничего нельзя будет туда провести, пока месяц не откроют заново.',

  'periods.caption': 'Все месяцы учёта, начиная с последнего.',
  'periods.colMonth': 'Месяц',
  'periods.colCutoff': 'Дата отсечения',
  'periods.colEntries': 'Проводок',
  'periods.colState': 'Состояние',

  'periods.historyTitle': 'Что происходило с учётом',
  'periods.wasOpened': '{month} — открыт',
  'periods.wasClosed': '{month} — закрыт',
  'periods.wasReopened': '{month} — открыт заново',

  'leads.title': 'Обращения',
  'leads.show': 'Показывать',
  'leads.stillChasing': 'В работе',
  'leads.everything': 'Все',
  'leads.onlyMine': 'Только мои',
  'leads.take': 'Принять обращение',
  'leads.loading': 'Загрузка обращений…',
  'leads.denied':
    'У вас нет доступа к обращениям этой площадки. Если это ошибка, обратитесь к руководителю.',
  'leads.failed': 'Не удалось загрузить обращения.',
  'leads.openFailed': 'Не удалось открыть это обращение.',
  'leads.empty':
    'Обращений нет. Обращение появляется, как только кто-то звонит или приходит в салон.',

  'leads.colCustomer': 'Клиент',
  'leads.colAskedAbout': 'Интересовал',
  'leads.colCameFrom': 'Источник',
  'leads.colDays': 'Дней',
  'leads.colChasedBy': 'Ведёт',
  'leads.colStage': 'Этап',
  'leads.nothingSpecific': 'Ничего конкретного',
  'leads.nobodyYet': 'Пока никто',
  'leads.you': 'Вы',
  'leads.somebodyElse': 'Кто-то другой',
  'leads.count': {
    one: '{count} обращение',
    few: '{count} обращения',
    many: '{count} обращений',
    other: '{count} обращения',
  },
  'leads.countCapped': 'Первые {count} обращений. Возможно, есть ещё.',
  'leads.cappedNote':
    'Показаны первые {count}. Возможно, есть ещё — уточните фильтрами, пока нет постраничного вывода.',

  'leads.cameIn': 'поступило {date}',
  'leads.unclaimed': 'Пока никто не взял его в работу.',
  'leads.yoursToChase': 'Это обращение ведёте вы.',
  'leads.theirsToChase': 'Это обращение ведёт {name}.',
  'leads.putBack': 'Вернуть в общий список',
  'leads.iWillChase': 'Возьму себе',
  'leads.takeItOver': 'Перехватить',
  'leads.handTo': 'Передать',
  'leads.chooseColleague': 'Выберите коллегу',
  'leads.buildTheDeal': 'Создать сделку',
  'leads.whatHappened': 'Что происходило',
  'leads.finished':
    'Это обращение завершено. Если клиент вернётся позже, начнётся новое.',
  'leads.note': 'Заметка (останется в истории)',
  'leads.notePlaceholder': 'Оставил сообщение · приедет в субботу · купил в другом месте',
  'leads.reopenedHere':
    'Потерянное обращение, по которому клиент вернулся, открывается заново здесь, а не заводится повторно, — так первая попытка остаётся частью истории.',

  'leads.moveReopen': 'Открыть заново',
  'leads.moveStartChasing': 'Взять в работу',
  'leads.moveAppointment': 'Клиент приедет',
  'leads.moveWon': 'Клиент покупает',
  'leads.moveLost': 'Отметить как потерянное',

  'leads.captureTitle': 'Принять обращение',
  'leads.findCustomer': 'Найти клиента',
  'leads.whoIsAsking': 'Кто обращается',
  'leads.chooseSomebody': 'Выберите человека…',
  'leads.searchAboveNote':
    'Найдите его через поиск выше. Обращение должно быть привязано к человеку: если клиент новый, сначала заведите его на странице «Клиенты».',
  'leads.whichLocation': 'Какая площадка',
  'leads.chooseLocation': 'Выберите площадку…',
  'leads.onlyLocation':
    'Это обращение относится к площадке {name} ({code}) — единственной, где вы работаете.',
  'leads.howTheyReachedUs': 'Как клиент с нами связался',
  'leads.carAskedAbout': 'Интересующий автомобиль (необязательно)',
  'leads.whatTheySaid': 'Что сказал клиент',
  'leads.whatTheySaidPlaceholder': 'Бюджет, трейд-ин, к какому сроку нужен…',
  'leads.save': 'Сохранить обращение',
  'leads.locationsFailed': 'Не удалось загрузить ваши площадки.',
  'leads.lookupFailed': 'Не удалось выполнить поиск.',
  'leads.saveFailed': 'Не удалось сохранить обращение.',

  'deals.title': 'Сделки',
  'deals.show': 'Показывать',
  'deals.stillWorked': 'В работе',
  'deals.everything': 'Все',
  'deals.start': 'Создать сделку',
  'deals.loading': 'Загрузка сделок…',
  'deals.denied':
    'У вас нет доступа к сделкам этой площадки. Если это ошибка, обратитесь к руководителю.',
  'deals.failed': 'Не удалось загрузить сделки.',
  'deals.openFailed': 'Не удалось открыть эту сделку.',
  'deals.empty': 'Сделок нет. Сделка появляется, когда автомобиль просчитан под клиента.',
  'deals.documentFailed': 'Не удалось открыть документ.',

  'deals.colCustomer': 'Клиент',
  'deals.colVehicle': 'Автомобиль',
  'deals.colStock': 'Складской №',
  'deals.colDue': 'К оплате',
  'deals.colStage': 'Этап',
  'deals.count': {
    one: '{count} сделка',
    few: '{count} сделки',
    many: '{count} сделок',
    other: '{count} сделки',
  },
  'deals.countCapped': 'Первые {count} сделок. Возможно, есть ещё.',
  'deals.cappedNote':
    'Показаны первые {count}. Возможно, есть ещё — уточните фильтром, пока нет постраничного вывода.',

  'deals.printOrder': 'Распечатать заказ',
  'deals.stockLine': 'Складской № {stock}',
  'deals.numbersCaption': 'Расчёт по этой сделке',
  'deals.colLine': 'Строка',
  'deals.colDescription': 'Наименование',
  'deals.colAmount': 'Сумма',
  'deals.lineProduct': 'Продукт',
  'deals.lineTradeIn': 'Трейд-ин',
  'deals.owesMore': 'долг превышает стоимость автомобиля',
  'deals.dueFromCustomer': 'К оплате клиентом',
  'deals.frozen':
    'Расчёт зафиксирован. Он перестал быть редактируемым в момент отправки на согласование, чтобы руководитель согласовывал именно то, что ему показали.',
  'deals.whatHappened': 'Что происходило',

  'deals.finished': 'Сделка завершена. Больше с ней ничего сделать нельзя.',
  'deals.sendToManager': 'Отправить руководителю',
  'deals.approve': 'Согласовать',
  'deals.handOver': 'Выдать автомобиль',
  'deals.markLost': 'Отметить как потерянную',
  'deals.markedLostNote': 'Отмечена как потерянная в отделе продаж.',
  'deals.cannotApproveOwn':
    'Тот, кто собрал сделку, не может сам её согласовать. Если это вы — согласовать должен руководитель.',

  'deals.soldWithTheCar': 'Продано вместе с автомобилем',
  'deals.colProduct': 'Продукт',
  'deals.colPrice': 'Цена',
  'deals.colGross': 'Маржа',
  'deals.productGross': '{amount} маржи на том, что продано вместе с автомобилем.',

  'terms.title': 'Расчёт',
  'terms.caption': 'Строки этой сделки',
  'terms.colLine': 'Строка',
  'terms.colDescription': 'Наименование',
  'terms.colAmount': 'Сумма',
  'terms.remove': 'Удалить',
  'terms.lineKind': 'Тип строки {n}',
  'terms.lineDescription': 'Наименование строки {n}',
  'terms.lineAmount': 'Сумма строки {n}',
  'terms.addLine': 'Добавить строку',
  'terms.addTradeIn': 'Добавить трейд-ин',
  'terms.dropTradeIn': 'Всё-таки без трейд-ина',
  'terms.tradeInTitle': 'Трейд-ин',
  'terms.whatTheyTrade': 'Что сдают',
  'terms.whatWeAllow': 'Сколько засчитываем',
  'terms.whatIsOwed': 'Остаток долга по нему',
  'terms.negativeEquity':
    'Долг по автомобилю больше засчитанной суммы, поэтому разница добавляется к этой сделке.',
  'terms.save': 'Сохранить расчёт',
  'terms.rejected': 'Этот расчёт не принят.',
  'terms.needsPrice':
    'В любой сделке должна быть цена самого автомобиля, иначе её нельзя сохранить.',

  'products.title': 'Продано вместе с автомобилем',
  'products.loading': 'Загрузка доступных продуктов…',
  'products.none':
    'Продукты для продажи не настроены. Их добавляет руководитель в каталоге F&I.',
  'products.lede':
    'Цены подставляются из каталога, и вы можете их изменить: в сделке сохранится именно то, что вы введёте здесь, а последующие изменения прайса её не затронут.',
  'products.colSell': 'Продать',
  'products.colProduct': 'Продукт',
  'products.colPrice': 'Цена',
  'products.colCost': 'Себестоимость',
  'products.colGross': 'Маржа',
  'products.sellThis': 'Продать: {product}',
  'products.priceFor': 'Цена: {product}',
  'products.costOf': 'Себестоимость: {product}',
  'products.termMonths': {
    one: '{count} месяц',
    few: '{count} месяца',
    many: '{count} месяцев',
    other: '{count} месяца',
  },
  'products.withdrawn': 'больше не предлагается',
  'products.nothingSelected': 'Ничего не выбрано.',
  'products.addedToDeal': '{added} добавлено к сделке, маржа составит {gross}.',
  'products.saveFailed': 'Сохранить не удалось.',
  'products.saveWhatIsSold': 'Сохранить продаваемое',

  'startDeal.title': 'Создать сделку',
  'startDeal.findBuyer': 'Найти покупателя',
  'startDeal.buyer': 'Кто покупает',
  'startDeal.chooseBuyer': 'Выберите человека…',
  'startDeal.searchAbove':
    'Найдите его через поиск выше. Если клиент новый, заведите его на странице «Клиенты».',
  'startDeal.fromEnquiry': 'По обращению клиента {name}. Сделка будет к нему привязана.',
  'startDeal.thatCustomer': 'этот клиент',
  'startDeal.whichCar': 'Какой автомобиль',
  'startDeal.chooseCar': 'Выберите автомобиль…',
  'startDeal.nothingAvailable':
    'Сейчас на площадке нет доступных автомобилей. Машина, занятая другой сделкой, забронирована до её завершения.',
  'startDeal.chooseCarFirst': 'Сначала выберите автомобиль.',
  'startDeal.submit': 'Создать сделку',
  'startDeal.starting': 'Создание…',
  'startDeal.failed': 'Не удалось создать сделку.',
  'startDeal.stockFailed': 'Не удалось загрузить список автомобилей.',
  'startDeal.customerFailed': 'Не удалось прочитать карточку клиента.',
};
