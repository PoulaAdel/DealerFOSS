// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ru — Russian.
//
// Usage:
//   Loaded by shared/i18n/index.tsx and selected by the language
//   picker. Never imported by a screen — a screen calls t(key), and
//   which catalogue answers is not its business.
//
// Coding Instructions:
//   The vocabulary is what a Russian дилерский центр uses, not a dictionary
//   rendering of the English. A lead is an *обращение* (the CRM word), the
//   service department is *сервис* rather than "мастерская", and vehicle
//   stock is *склад* — which means warehouse elsewhere but is the trade word
//   for cars on the lot. Accounting follows РСБУ usage:
//   *оборотно-сальдовая ведомость* is the trial balance.
//
//   Formal *вы*, lower case, as Russian business software writes it.
//
//   Russian has FOUR plural categories and they are not optional. `2 сделки`
//   and `5 сделок` differ, and so do `21 сделка` and `22 сделки`. Every
//   counted noun in this file is a plural entry, chosen by Intl.PluralRules.

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
  'nav.passkeys': 'Ключи доступа',

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

  'enum.servicePayType.CustomerPay': 'Платит клиент',
  'enum.servicePayType.Warranty': 'Гарантия',
  'enum.servicePayType.Internal': 'Внутренние',

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
  'stock.detailFor': 'Складской номер {stock}',
  'stock.cost': 'Себестоимость',
  'stock.costUnknown': 'не указана',
  'stock.acquired': 'Принят на склад',
  'stock.historyTitle': 'Что с ним происходило',
  'stock.historyEmpty': 'По этому автомобилю пока ничего не записано.',
  'stock.takenIn': 'Принят на склад со статусом «{to}»',
  'stock.moved': '{from} → {to}',

  'nav.reports': 'Отчёты',

  'reports.title': 'Что дал месяц',
  'reports.month': 'Месяц',
  'reports.loading': 'Считаем цифры…',
  'reports.denied': 'У вас нет доступа к цифрам. Обратитесь к тому, кто ведёт учёт.',
  'reports.profitTitle': 'Отчёт о прибылях и убытках',
  'reports.department': 'Подразделение',
  'reports.revenue': 'Выручка',
  'reports.cost': 'Себестоимость',
  'reports.gross': 'Валовая прибыль',
  'reports.grossProfit': 'Валовая прибыль всего',
  'reports.overheads': 'Во что обходится работа',
  'reports.totalOverheads': 'Итого расходов',
  'reports.netProfit': 'Чистая прибыль',
  'reports.sheetTitle': 'Сколько стоит бизнес',
  'reports.sheetBalances': 'Сходится: то, чем бизнес владеет, равно тому, что он должен, плюс его стоимость.',
  'reports.sheetDoesNotBalance':
    'Не сходится. Проведено что-то, что этот отчёт не может отнести ни к чему, — считайте все цифры ниже сомнительными, пока кто-нибудь не выяснит, что именно.',
  'reports.assets': 'Чем владеет',
  'reports.liabilities': 'Что должен',
  'reports.equity': 'Что вложили владельцы',
  'reports.total': 'Итого',
  'reports.nothingHere': 'Здесь пока ничего не записано.',
  'reports.earningsToDate': 'Заработано с начала',
  'reports.earningsNote':
    'Держим отдельной строкой, а не прибавляем к вложенному, потому что ни один год ещё не закрыт.',

  'entry.title': 'Записать проводку',
  'entry.note':
    'Для того, что больше нигде не записывается: расход, вложенный капитал, исправление. Обе стороны должны дать одну сумму.',
  'entry.whichLocation': 'Какая площадка',
  'entry.chooseLocation': 'Выберите площадку…',
  'entry.when': 'Когда это было',
  'entry.what': 'За что',
  'entry.account': 'Счёт',
  'entry.chooseAccount': 'Выберите счёт…',
  'entry.debit': 'Дебет',
  'entry.credit': 'Кредит',
  'entry.lineNote': 'Заметка',
  'entry.accountOnLine': 'Счёт в строке {line}',
  'entry.debitOnLine': 'Дебет в строке {line}',
  'entry.creditOnLine': 'Кредит в строке {line}',
  'entry.noteOnLine': 'Заметка в строке {line}',
  'entry.totals': 'Итоги',
  'entry.addLine': 'Добавить строку',
  'entry.outBy': 'Стороны расходятся на {amount}.',
  'entry.record': 'Записать',

  'enum.paymentMethod.Cash': 'Наличные',
  'enum.paymentMethod.Card': 'Карта',
  'enum.paymentMethod.BankTransfer': 'Банковский перевод',
  'enum.paymentMethod.Cheque': 'Чек',
  'enum.paymentMethod.Finance': 'Финансовая компания',

  'money.title': 'Сколько должны',
  'money.billed': 'Выставлено',
  'money.paid': 'Уже оплачено',
  'money.outstanding': 'Осталось',
  'money.settled': 'Оплачено полностью. Больше ничего не должны.',
  'money.owedFor': {
    one: 'Не оплачено {count} день.',
    few: 'Не оплачено {count} дня.',
    many: 'Не оплачено {count} дней.',
    other: 'Не оплачено {count} дня.',
  },
  'money.paymentsTitle': 'Что было оплачено',
  'money.howMuch': 'Сумма',
  'money.howPaid': 'Способ оплаты',
  'money.reference': 'Отметка (останется в записи)',
  'money.takeIt': 'Записать оплату',
  'stock.takeItIn': 'Принять машину на склад',
  'stock.confirmTakeIn': 'Принять',
  'stock.takeInTitle': 'Принять машину на склад',
  'stock.takeInNote':
    'Машина и её карточка создаются вместе: прибывающий автомобиль почти всегда тот, которого вы ещё не видели.',
  'stock.whichLocation': 'Какая площадка',
  'stock.chooseLocation': 'Выберите площадку…',
  'stock.vinOptional': 'VIN (необязательно)',
  'stock.modelYear': 'Год',
  'stock.make': 'Марка',
  'stock.model': 'Модель',
  'stock.trimOptional': 'Комплектация (необязательно)',
  'stock.costOptional': 'Себестоимость (необязательно)',
  'stock.costNote':
    'Себестоимость ставит машину на баланс. Оставьте пустым, если пока не знаете, — это запишется как «неизвестно», а не как ноль.',
  'stock.floorplanned': 'Эту машину оплачивает кредитор (floorplan)',
  'stock.moveTitle': 'Куда дальше',
  'stock.moveNote': 'Заметка (останется в записи)',
  'stock.moveTo': 'Перевести в «{to}»',
  'stock.soldNote': 'Эта машина продана. Чтобы отменить, сторнируйте сделку.',
  'stock.noMovesNote': 'Отсюда эту машину никуда перевести нельзя.',

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

  'enum.contactKind.Email': 'Эл. почта',
  'enum.contactKind.Phone': 'Телефон',
  'enum.contactKind.Mobile': 'Мобильный',

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
  'customers.cameFrom': 'Источник',
  'customers.waysToReach': 'Как связаться',
  'customers.primary': 'основной',
  'customers.address': 'Адрес',
  'customers.noAddress': 'Адрес не указан.',
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
  'leads.untouchedTitle': 'Ими никто не занимается',
  'leads.untouchedNote': {
    one: 'У {count} обращения нет ответственного.',
    few: 'У {count} обращений нет ответственного.',
    many: 'У {count} обращений нет ответственного.',
    other: 'У {count} обращения нет ответственного.',
  },
  'leads.noParticularCar': 'без конкретного автомобиля',
  'leads.waitingDays': {
    one: 'ждёт {count} день',
    few: 'ждёт {count} дня',
    many: 'ждёт {count} дней',
    other: 'ждёт {count} дня',
  },
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

  'tax.title': 'Налоги',
  'tax.lede':
    'Пока это не рассчитывается автоматически — введите то, что применимо. В каждой строке фиксируется, что её ввёл человек: именно это позже нужно видеть руководителю и аудитору.',
  'tax.workedOutFrom': 'Адрес, по которому рассчитывается налог',
  'tax.state': 'Штат или регион',
  'tax.county': 'Округ',
  'tax.postalCode': 'Почтовый индекс',
  'tax.country': 'Страна',
  'tax.colDescription': 'Налог',
  'tax.colJurisdiction': 'Юрисдикция',
  'tax.colBasis': 'База',
  'tax.colRate': 'Ставка, %',
  'tax.colAmount': 'Сумма',
  'tax.colSource': 'Источник',
  'tax.descriptionOfLine': 'Налог в строке {line}',
  'tax.jurisdictionOfLine': 'Юрисдикция в строке {line}',
  'tax.basisOfLine': 'База в строке {line}',
  'tax.rateOfLine': 'Ставка в строке {line}, в процентах',
  'tax.amountOfLine': 'Начисленный налог в строке {line}',
  'tax.none': 'По этой сделке налогов пока нет.',
  'tax.totalIs': 'Налоги по этой сделке: {total}.',
  'tax.totalLabel': 'Налоги',
  'tax.addLine': 'Добавить строку налога',
  'tax.save': 'Сохранить налоги',
  'tax.from.EnteredByPerson': 'ввёл человек',
  'tax.from.Pack': 'таблица ставок',
  'tax.from.Vendor': 'налоговый провайдер',
  'tax.fromPack': '{pack} v{version}',

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

  'staff.title': 'Сотрудники',
  'staff.loading': 'Загрузка списка сотрудников…',
  'staff.denied':
    'У вас нет доступа к списку сотрудников. Если он вам нужен, обратитесь к руководителю.',
  'staff.failed': 'Не удалось прочитать список сотрудников.',
  'staff.actionFailed': 'Не получилось.',
  'staff.empty': 'Пока никого нет.',
  'staff.add': 'Добавить сотрудника',
  'staff.caption': 'Все, чей доступ распространяется на площадку, где вы работаете.',
  'staff.colName': 'Имя',
  'staff.colEmail': 'Почта',
  'staff.colHolds': 'Права',
  'staff.colSecondFactor': 'Второй фактор',
  'staff.colState': 'Состояние',
  'staff.holdsNothing': 'Пока нет',

  'staff.stateStopped': 'Заблокирован',
  'staff.stateAwaiting': 'Ожидает первый пароль',
  'staff.stateWorking': 'Работает',

  'staff.codeFor': 'Код для {name}',
  'staff.readItOut':
    'Продиктуйте его сотруднику. С этим кодом он сам задаст пароль на экране входа — никто другой его не вводит, включая вас.',
  'staff.onlyTimeShown': 'Это единственный раз, когда код можно показать.',
  'staff.onlyTimeShownRest':
    'Хранится только его хеш, поэтому посмотреть код заново нельзя. Если он потерялся, выпустите новый — старый перестанет работать. Срок действия истекает {expires}.',
  'staff.passedItOn': 'Я передал код',

  'staff.addTitle': 'Добавить сотрудника',
  'staff.addLede':
    'Он не сможет войти, пока не задаст пароль по коду, который здесь появится. Вы никогда не видите и не выбираете его пароль.',
  'staff.name': 'Имя',
  'staff.email': 'Почта',
  'staff.addAndMakeCode': 'Добавить и выпустить код',

  'staff.hasSecondFactor': 'второй фактор настроен',
  'staff.noSecondFactor': 'второй фактор не настроен',
  'staff.whatTheyHold': 'Какие права у него есть',
  'staff.holdsNothingYet': 'Пока никаких — он сможет войти и не увидит ничего.',
  'staff.everywhere': 'везде',
  'staff.oneLocation': 'одна площадка',
  'staff.takeItAway': 'Отозвать',
  'staff.giveARole': 'Выдать роль',
  'staff.role': 'Роль',
  'staff.chooseRole': 'Выберите роль',
  'staff.holdingGrants': 'Эта роль даёт: {permissions}',
  'staff.andObligesSecondFactor': ' — и обязывает настроить второй фактор.',
  'staff.where': 'Где',
  'staff.everywhereInOrg': 'Везде в группе',
  'staff.giveThem': 'Выдать',
  'staff.makeNewCode': 'Выпустить новый код',
  'staff.stopAccount': 'Заблокировать учётную запись',
  'staff.letThemBackIn': 'Разблокировать',
  'staff.stoppingNote':
    'Блокировка завершает сеансы сотрудника уже на следующем запросе и ничего не удаляет — его имя должно оставаться рядом с выполненной работой.',

  'parts.title': 'Запчасти',
  'parts.loading': 'Загрузка каталога запчастей…',
  'parts.denied':
    'У вас нет доступа к запчастям этой площадки. Если это ошибка, обратитесь к руководителю.',
  'parts.failed': 'Не удалось прочитать каталог.',
  'parts.actionFailed': 'Не получилось.',
  'parts.add': 'Добавить запчасть',
  'parts.find': 'Найти запчасть',
  'parts.findPlaceholder': 'Артикул или описание',
  'parts.findHint':
    'Артикул находится в любом написании: MZ-690411, mz690411 и MZ 690 411 приведут к одной и той же запчасти.',
  'parts.catalogueEmpty': 'В каталоге пока пусто.',
  'parts.noMatches': 'Ничего не найдено.',
  'parts.caption': 'Запчасти и их остатки на площадках, которые вы обслуживаете.',
  'parts.colNumber': 'Артикул',
  'parts.colDescription': 'Описание',
  'parts.colWhere': 'Где',
  'parts.colOnHand': 'В наличии',
  'parts.colCostEach': 'Цена за штуку',
  'parts.notStocked': 'Не хранится',
  'parts.oneLocation': 'одна площадка',
  'parts.noneOnHand': 'Нет',

  'parts.costingTitle': 'Как оцениваются запчасти',
  'parts.costingMethod': 'Метод',
  'parts.costingFutureOnly': 'только к будущим продажам',
  'parts.costingNote':
    'Уже выставленные работы сохраняют ту себестоимость, по которой были проданы: изменение здесь не может пересчитать месяц, по которому уже сдана отчётность.',
  'parts.costingApplies': 'Это относится {futureOnly}. {rest}',

  'parts.addTitle': 'Добавить запчасть',
  'parts.addLede':
    'Артикул обозначает одну и ту же деталь на всех площадках, поэтому это изменение на уровне группы. Сам остаток принадлежит тому складу, на который его оприходовали.',
  'parts.partNumber': 'Артикул',
  'parts.description': 'Описание',
  'parts.addIt': 'Добавить',

  'parts.noneVisible': 'Этой детали нет ни на одном доступном вам складе.',
  'parts.shelfHeading': '{code} — {quantity} в наличии по {cost} за штуку',
  'parts.deliveriesCaption': 'Поступления {part} на {code}.',
  'parts.colReceived': 'Поступление',
  'parts.colNote': 'Документ',
  'parts.colCameIn': 'Пришло',
  'parts.colLeft': 'Осталось',

  'parts.bookIn': 'Оприходовать поставку',
  'parts.ontoWhichShelf': 'На какой склад',
  'parts.howMany': 'Сколько',
  'parts.costEach': 'Цена за штуку',
  'parts.deliveryNote': 'Накладная',
  'parts.bookItIn': 'Оприходовать',

  'dash.soFar': 'С начала месяца.',
  'dash.asFinished': 'Месяц в том виде, в каком он завершился.',
  'dash.whichMonth': 'Какой месяц',
  'dash.previousMonth': 'Предыдущий месяц',
  'dash.nextMonth': 'Следующий месяц',
  'dash.previousMonthTitle': 'Предыдущий месяц ( [ )',
  'dash.nextMonthTitle': 'Следующий месяц ( ] )',
  'dash.rooftop': 'Площадка',
  'dash.everywhere': 'Все доступные мне',
  'dash.loading': 'Подсчёт месяца…',
  'dash.denied': 'У вас нет доступа ни к одной цифре на этой сводке.',
  'dash.failed': 'Не удалось загрузить месяц.',

  'dash.withheldTrading':
    'Суммы за этот месяц вам недоступны, поэтому ниже показаны только данные по складу.',
  'dash.withheldStock': 'Склад вам недоступен, поэтому за месяц показано только проданное.',

  'dash.booksOpen': 'Период открыт, поэтому эти цифры ещё могут измениться.',
  'dash.booksClosed': 'Период закрыт. Это те цифры, которые были сданы.',
  'dash.booksClosedOn': 'Период закрыт {date}. Это те цифры, которые были сданы.',
  'dash.booksNotOpened': 'Период за этот месяц никто не открывал, провести в него ничего нельзя.',
  'dash.booksUnknown': 'Состояние периода вам недоступно.',

  'dash.whatTheMonthMade': 'Что принёс месяц',
  'dash.totalGross': 'Валовая прибыль',
  'dash.financeShort': 'F&I',
  'dash.whatSold': 'Что продано',
  'dash.carsDelivered': 'Выдано автомобилей',
  'dash.jobsInvoiced': 'Заказ-нарядов выставлено',
  'dash.grossPerCar': 'Прибыль на автомобиль',
  'dash.frontAndBack': 'автомобиль и F&I вместе',

  'dash.whereGrossCameFrom': 'Откуда пришла прибыль',
  'dash.colDepartment': 'Подразделение',
  'dash.colRevenue': 'Выручка',
  'dash.colCost': 'Себестоимость',
  'dash.colGross': 'Прибыль',
  'dash.colMargin': 'Рентабельность',
  'dash.total': 'Итого',

  'dash.howOldTheStockIs': 'Возраст склада',
  'dash.unsoldAsAt': ' — {count} непроданных, на {date}',
  'dash.nothingUnsold': 'На площадке нет непроданных автомобилей.',
  'dash.standingLongest': 'Стоят дольше всех',
  'dash.colStock': 'Складской №',
  'dash.colVehicle': 'Автомобиль',
  'dash.colStatus': 'Статус',
  'dash.colDays': 'Дней',
  'dash.estimatedAge':
    'Дата поступления не записана, поэтому отсчёт идёт с момента внесения в систему.',
  'dash.estimatedAgeNote':
    '* отсчёт с момента внесения автомобиля в систему, так как дата поступления не записана.',

  'workshop.title': 'Сервис',
  'workshop.loading': 'Загрузка заказ-нарядов…',
  'workshop.denied':
    'У вас нет доступа к сервису этой площадки. Если это ошибка, обратитесь к руководителю.',
  'workshop.failed': 'Не удалось прочитать список заказ-нарядов.',
  'workshop.actionFailed': 'Не получилось.',
  'workshop.openOnly': 'Только незакрытые',
  'workshop.nothingOpen': 'Сейчас в сервисе ничего нет.',
  'workshop.empty': 'Заказ-нарядов пока нет.',
  'workshop.caption': 'Заказ-наряды на площадках, которые вы обслуживаете.',
  'workshop.colJob': 'Заказ-наряд',
  'workshop.colCustomer': 'Клиент',
  'workshop.colVehicle': 'Автомобиль',
  'workshop.colCameInFor': 'С чем обратились',
  'workshop.colWaiting': 'Ожидание',
  'workshop.colDue': 'К оплате',
  'workshop.colStage': 'Этап',

  'workshop.waitingTitle': 'Ждут ответа клиента',
  'workshop.waitingNote': {
    one: 'В одном заказ-наряде есть работы, которые никто не согласовал. Его нельзя выставить к оплате, пока кто-нибудь не позвонит.',
    few: 'В {count} заказ-нарядах есть работы, которые никто не согласовал. Ни один из них нельзя выставить к оплате, пока кто-нибудь не позвонит.',
    many: 'В {count} заказ-нарядах есть работы, которые никто не согласовал. Ни один из них нельзя выставить к оплате, пока кто-нибудь не позвонит.',
    other:
      'В {count} заказ-нарядах есть работы, которые никто не согласовал. Ни один из них нельзя выставить к оплате, пока кто-нибудь не позвонит.',
  },
  'workshop.toAskAbout': {
    one: '{count} работа на согласование',
    few: '{count} работы на согласование',
    many: '{count} работ на согласование',
    other: '{count} работы на согласование',
  },
  'workshop.pendingNote': {
    one: 'Одна работа ждёт согласования клиента. Её нельзя выставить к оплате до его ответа.',
    few: '{count} работы ждут согласования клиента. Ни одну нельзя выставить к оплате до его ответа.',
    many: '{count} работ ждут согласования клиента. Ни одну нельзя выставить к оплате до его ответа.',
    other:
      '{count} работы ждут согласования клиента. Ни одну нельзя выставить к оплате до его ответа.',
  },

  'workshop.stageBooked': 'Записан',
  'workshop.stageInProgress': 'В работе',
  'workshop.stageCompleted': 'Работы выполнены',
  'workshop.stageInvoiced': 'Выставлен счёт',
  'workshop.stageCancelled': 'Отменён',

  'workshop.moveInProgress': 'Начать работы',
  'workshop.moveCompleted': 'Работы выполнены',
  'workshop.moveInvoiced': 'Выставить счёт',
  'workshop.moveCancelled': 'Отменить заказ-наряд',
  'workshop.moveBooked': 'Вернуть в «записан»',

  'workshop.miles': '{count} миль',
  'workshop.bookedIn': 'принят {date}',
  'workshop.printJobSheet': 'Распечатать заказ-наряд',
  'workshop.printInvoice': 'Распечатать счёт',
  'workshop.whatHappened': 'Что происходило',

  'workshop.theWork': 'Работы',
  'workshop.nothingWrittenUp': 'Пока ничего не записано.',
  'workshop.colWhat': 'Что',
  'workshop.colDetail': 'Подробности',
  'workshop.colAgreed': 'Согласовано?',
  'workshop.colAmount': 'Сумма',
  'workshop.nobodyAsked': 'Не спрашивали',
  'workshop.saidNo': 'Отказался',
  'workshop.agreed': 'Согласовано',
  'workshop.iRangThem': 'Я позвонил',
  'workshop.notNow': 'Не сейчас',
  'workshop.howObtained': 'Как получено согласие',
  'workshop.howObtainedPlaceholder': 'Звонок в 10:40, говорил с г-жой Окафор',
  'workshop.theySaidYes': 'Клиент согласился',
  'workshop.theySaidNo': 'Клиент отказался',
  'workshop.hoursAtRate': '{hours} ч по {rate}',

  'workshop.writeUpMore': 'Добавить работы',
  'workshop.lineKind': 'Что',
  'workshop.lineDescription': 'Описание',
  'workshop.hours': 'Часы',
  'workshop.rate': 'Ставка',
  'workshop.amount': 'Сумма',
  'workshop.addLine': 'Добавить',

  'workshop.totalsCaption': 'Итог по заказ-наряду.',
  'workshop.totalLabour': 'Работы',
  'workshop.totalParts': 'Запчасти',
  'workshop.totalSublet': 'Сторонние работы',
  'workshop.totalDue': 'К оплате',

  'workshop.whoIsOnIt': 'Кто выполняет',
  'workshop.nobodyYet': 'Пока никто',
  'workshop.caption2': 'Работы на площадках, которые вы обслуживаете, начиная с последних, не более {limit}.',
  'workshop.invoicedNothingMore': 'Счёт выставлен {date}. Работа закончена; что осталось оплатить — ниже.',
  'workshop.jobFinished': 'Этот заказ-наряд завершён.',
  'workshop.assignedElsewhere':
    'Назначен сотруднику, которого нет в вашем списке, — возможно, он работает на другой площадке.',
  'workshop.removeLine': 'Убрать',
  'workshop.moveNote': 'Примечание (останется в записи)',
  'workshop.howObtainedHint':
    'Именно это важно, если счёт когда-нибудь оспорят. Укажите, с кем и когда вы говорили.',
  'workshop.cappedNote': 'Первые {limit}, самые новые сверху — возможно, есть и другие.',
  'workshop.toAsk': {
    one: '{count} на согласование',
    few: '{count} на согласование',
    many: '{count} на согласование',
    other: '{count} на согласование',
  },
  'workshop.labourReport': 'Отчёт по работам',

  'workshop.colWhoPays': 'Кто платит',
  'workshop.linePayType': 'Кто платит',
  'workshop.notCustomersCall': 'Соглашается не клиент',
  'workshop.totalWarranty': 'Гарантия',
  'workshop.totalInternal': 'Внутренние',
  'workshop.totalWork': 'Все работы',
  'workshop.writeUpNote':
    'Всё, что добавлено сейчас, требует ответа клиента, прежде чем это можно выставить к оплате, — в этом и смысл. Запишите, пока видите это перед собой.',
  'workshop.writeUpNoteOther':
    'Звонить клиенту по этому поводу не нужно: платит не он. Работа всё равно попадёт в заказ-наряд, чтобы она была зафиксирована, а часы — учтены.',

  // --- Что продал сервис -----------------------------------------------------
  'labour.title': 'Работы',
  'labour.from': 'С',
  'labour.to': 'По',
  'labour.backToWorkshop': 'Вернуться в сервис',
  'labour.loading': 'Считаем показатели по работам…',
  'labour.denied':
    'У вас нет доступа к показателям этого сервиса. Обратитесь к руководителю, если считаете это ошибкой.',

  'labour.headlineCaption':
    'Проданные часы, выручка по работам и то, сколько на деле принёс час.',
  'labour.hoursSold': 'Проданные часы',
  'labour.revenue': 'Выручка по работам',
  'labour.effectiveRate': 'Сколько принёс час',

  'labour.byTechnician': 'По механикам',
  'labour.technicianCaption': 'Часы и выручка каждого механика за период.',
  'labour.colWho': 'Механик',
  'labour.colHours': 'Часы',
  'labour.colRevenue': 'Выручка',
  'labour.colRate': 'За час',
  'labour.nobodyCredited': 'Никому не засчитано',
  'labour.notNamed': 'Без имени',
  'labour.namesUnavailable':
    'Механики здесь не названы, потому что вам недоступен список сотрудников. Часы и суммы при этом верны.',
  'labour.nothingInvoiced': 'За этот период ничего не выставлено к оплате, поэтому часов нет.',

  'labour.byPayer': 'Кто платил',
  'labour.payerCaption': 'Часы и выручка в разбивке по тому, кто оплачивает работы.',
  'labour.colPayer': 'Оплачивает',

  'labour.notMeasuredTitle': 'Что здесь не измеряется',
  'labour.notMeasuredWhy':
    'По этим двум показателям обычно и судят о сервисе, но ни один из них нельзя честно вывести из того, что хранит эта система. Обоим нужно то, чего сюда никто никогда не вносил.',
  'labour.noEfficiency':
    'Эффективность — выработанные часы к доступным. Графика смен нет, поэтому делить не на что.',
  'labour.noProductivity':
    'Производительность — оплаченные часы к отработанным по табелю. Табеля нет, поэтому делить не на что.',
  'labour.period':
    'Считается по работам, выставленным к оплате с {from} по {to}. Незавершённые работы выручкой не являются.',

  // --- Отзывные кампании -----------------------------------------------------
  'recalls.title': 'Отзывные кампании',
  'recalls.onRequest':
    'Это запрос в надзорный орган по безопасности дорожного движения, поэтому он выполняется только по вашей команде.',
  'recalls.check': 'Проверить отзывные кампании',
  'recalls.checkAgain': 'Проверить снова',
  'recalls.checking': 'Запрашиваем надзорный орган…',
  'recalls.caveat':
    'Это кампании, опубликованные для {make} {model} {year} года. Реестр ведётся по моделям, а не по автомобилям: он не говорит, выполнены ли работы на этой машине, — об этом знает только производитель.',
  'recalls.noneFound':
    'Для этой модели кампаний не опубликовано. Это не то же самое, что проверить сам автомобиль.',
  'recalls.doNotDrive': 'Не эксплуатировать',
  'recalls.parkOutside': 'Держать на открытом воздухе',
  'recalls.remedy': 'Что делают: {remedy}',

  // --- Ключи доступа ---------------------------------------------------------
  'passkey.useOne': 'Войти по ключу доступа',
  'passkey.needDealerGroup':
    'Сначала укажите свою группу — от неё зависит, в какой дилерский центр вы входите.',
  'passkey.ceremonyFailed':
    'Ваше устройство не смогло это завершить. Попробуйте ещё раз или войдите по паролю.',

  'passkey.title': 'Ключи доступа',
  'passkey.lede':
    'Ключ доступа впускает вас через телефон или ноутбук, который вы и так разблокируете, вместо пароля. Пароль при этом продолжает работать, и ничто на этом экране его не отменяет.',
  'passkey.addTitle': 'Добавить ключ доступа',
  'passkey.addNote':
    'Устройство попросит подтверждение. Ничего секретного его не покидает — только открытый ключ, бесполезный для того, кто снимет с него копию.',
  'passkey.label': 'Как его назвать',
  'passkey.labelPlaceholder': 'Рабочий ноутбук',
  'passkey.labelHint':
    'Это имя вы увидите, когда будете его удалять, поэтому назовите устройство, а не себя.',
  'passkey.add': 'Добавить',
  'passkey.adding': 'Ждём ваше устройство…',
  'passkey.added': '{label} зарегистрирован.',
  'passkey.unsupported':
    'Этот браузер не умеет работать с ключами доступа. Большинство умеет — по соединению, отличному от обычного http.',

  'passkey.yoursTitle': 'Ваши ключи доступа',
  'passkey.loading': 'Загружаем ваши ключи доступа…',
  'passkey.none': 'У вас пока нет ключей доступа.',
  'passkey.caption': {
    one: '{count} ключ доступа в этой учётной записи',
    few: '{count} ключа доступа в этой учётной записи',
    many: '{count} ключей доступа в этой учётной записи',
    other: '{count} ключа доступа в этой учётной записи',
  },
  'passkey.colLabel': 'Название',
  'passkey.colAdded': 'Добавлен',
  'passkey.colLastUsed': 'Последнее использование',
  'passkey.neverUsed': 'Ни разу не использован',
  'passkey.forget': 'Удалить',
  'passkey.forgetConfirm':
    'Удалить {label}? Это устройство больше не сможет вас впускать, и отменить это нельзя.',
  'passkey.forgot': '{label} удалён.',

  // --- Возврат доступа к учётной записи -------------------------------------------
  'recover.link': 'Я забыл пароль',
  'recover.title': 'Вернуть доступ',
  'recover.lede':
    'Выберите, чем вы можете подтвердить, что запись ваша. В любом случае новый пароль вы зададите прямо здесь.',
  'recover.withAuthenticator': 'С приложением-аутентификатором',
  'recover.withAuthenticatorHint':
    'Для тех, у кого настроен двухэтапный вход. Код из приложения или один из сохранённых резервных кодов.',
  'recover.withCode': 'С кодом от руководителя',
  'recover.withCodeHint':
    'Попросите руководителя выдать его на экране «Сотрудники». Он продиктует код; тот действует четыре часа.',
  'recover.email': 'Электронная почта',
  'recover.codeFromApp': 'Код из приложения-аутентификатора',
  'recover.codeFromManager': 'Код, который дал руководитель',
  'recover.newPassword': 'Новый пароль',
  'recover.newPasswordAgain': 'Повторите новый пароль',
  'recover.mismatch': 'Эти два пароля не совпадают.',
  'recover.submit': 'Задать пароль',
  'recover.working': 'Сохранение…',
  'recover.doneTitle': 'Готово',
  'recover.doneLede':
    'Пароль изменён, и все устройства, где был выполнен вход, отключены. Войдите заново с новым паролем.',
  'recover.toSignIn': 'Перейти ко входу',
  'recover.back': 'Выбрать другой способ',
  'recover.noMethods':
    'На этой установке нет способа вернуть доступ самостоятельно. Попросите руководителя завести вас заново.',

  'staff.resetTitle': 'Сбросить пароль',
  'staff.reset': 'Выдать код сброса',
  'staff.resetting': 'Выдача…',
  'staff.resetNote':
    'Это позволит человеку снова войти под собой. Продиктуйте код — он показывается один раз и действует четыре часа.',
  'staff.resetWarning':
    'Вы передаёте возможность входить от имени этого человека. Убедитесь, что говорите именно с ним.',
  'staff.resetIssued': 'Код сброса выдан {when} и пока не использован.',
  'staff.resetDone': 'Я продиктовал код',

  // The signal band on the deal desk: approvals somebody is blocking.
  'deals.awaitingTitle': 'Ждут руководителя',
  'deals.awaitingNote': {
    one: '{count} сделка никем не согласована и до этого не может быть выдана.',
    few: '{count} сделки никем не согласованы и до этого не могут быть выданы.',
    many: '{count} сделок никем не согласованы и до этого не могут быть выданы.',
    other: '{count} сделки никем не согласованы и до этого не могут быть выданы.',
  },
  // Changing a value where it is written. See shared/InlineEdit.tsx.
  'inline.changeThis': '{label}: {value}. Нажмите, чтобы изменить.',
  'inline.saved': 'Сохранено',

  // --- Журнал записи в сервис ---------------------------------------------------
  // Машины, которых ждут, но которых ещё нет. «Запись», а не «слот»: сервис
  // записывает на утро, а не на сорок минут.
  'enum.appointmentStatus.Scheduled': 'Ожидается',
  'enum.appointmentStatus.Arrived': 'Приехала',
  'enum.appointmentStatus.NoShow': 'Не приехала',
  'enum.appointmentStatus.Cancelled': 'Отменена',

  'diary.title': 'Ожидаются',
  'diary.loading': 'Загрузка журнала записи…',
  'diary.empty': 'Записей нет. Журнал пуст.',
  'diary.count': {
    one: 'ожидается {count} машина',
    few: 'ожидаются {count} машины',
    many: 'ожидается {count} машин',
    other: 'ожидается {count} машины',
  },
  'diary.dayLoad': {
    one: '{count} машина, {hours} ч работы',
    few: '{count} машины, {hours} ч работы',
    many: '{count} машин, {hours} ч работы',
    other: '{count} машины, {hours} ч работы',
  },
  'diary.dayLoadSome': {
    one: '{count} машина, {hours} ч в плане и {unestimated} без оценки',
    few: '{count} машины, {hours} ч в плане и {unestimated} без оценки',
    many: '{count} машин, {hours} ч в плане и {unestimated} без оценки',
    other: '{count} машины, {hours} ч в плане и {unestimated} без оценки',
  },
  'diary.unestimated': 'Без оценки',
  'diary.colWhen': 'Когда',
  'diary.colCustomer': 'Клиент',
  'diary.colVehicle': 'Машина',
  'diary.colReason': 'По какому поводу',
  'diary.colHours': 'Оценка',
  'diary.colWhat': 'Что дальше',
  'diary.itsHere': 'Машина здесь',
  'diary.arriving': 'Открываем заказ-наряд…',
  'diary.didNotCome': 'Не приехала',
  'diary.becameJob': 'Заказ-наряд {number}',

  'diary.book': 'Записать машину',
  'diary.bookTitle': 'Записать машину',
  'diary.customer': 'Клиент',
  'diary.vehicle': 'Машина',
  'diary.when': 'Когда',
  'diary.hours': 'Ожидаемые часы работы',
  'diary.hoursHint': 'Оставьте пустым, если оценки ещё нет.',
  'diary.reason': 'С чем приезжает машина',
  'diary.reasonPlaceholder': 'Годовое ТО',
  'diary.take': 'Записать',
  'diary.taking': 'Записываем…',
  'diary.pickCustomer': 'Выберите клиента',
  'diary.pickVehicle': 'Выберите машину',

  // --- Консоль управления установкой ------------------------------------------
  // Словарь намеренно отличается от дилерского: тот, кто читает эти экраны,
  // обслуживает установку. «Дилер» здесь — учётная запись на сервере, а не
  // площадка с автомобилями.
  'admin.badge': 'Администрирование',
  'admin.navDealerships': 'Дилеры',
  'admin.navSupportAccess': 'Доступ поддержки',

  'admin.signInLede': 'Это вход в установку, а не в дилерский центр.',
  'admin.signInCode': 'Код из приложения-аутентификатора',
  'admin.signInCodeNote': 'Оставьте пустым, только если вы ещё не настроили его.',

  'admin.secondFactorTitle': 'Настройте второй фактор',
  'admin.secondFactorRequired':
    'Для учётных записей администратора он обязателен. Пока вы его не настроите, этот экран — единственный доступный.',
  'admin.secondFactorIntro':
    'Эта учётная запись может войти в любой дилерский центр установки, поэтому одного пароля для её защиты недостаточно.',
  'admin.noRecoveryCodes':
    'Для учётной записи администратора резервных кодов нет. Если вы потеряете этот телефон, сбросить второй фактор сможет только тот, у кого есть доступ к базе данных.',

  'admin.dealerships': 'Дилеры',
  'admin.setUpDealership': 'Создать дилера',
  'admin.suspendConfirm':
    'Приостановить «{name}»? Все сотрудники будут немедленно отключены и не смогут работать, пока доступ не восстановят.',
  'admin.dealershipReady': '«{name}» готов',
  'admin.firstManager':
    'Первый руководитель — {email}. Продиктуйте ему этот код: с ним он сам задаст пароль на экране входа.',
  'admin.codeShownOnce': 'Показать его можно только один раз.',
  'admin.codeShownOnceWhy':
    'Хранится только зашифрованная копия, поэтому посмотреть код заново нельзя — если он потерян, руководителю можно выдать новый на экране «Сотрудники» самого дилера. Книги открыты, работать можно сразу.',
  'admin.passedItOn': 'Я передал код',
  'admin.loadingDealerships': 'Загрузка списка дилеров…',
  'admin.noDealerships': 'На этой установке ещё нет дилеров. Создайте первого выше.',
  'admin.dealershipsCaption': {
    one: '{count} дилер на этой установке',
    few: '{count} дилера на этой установке',
    many: '{count} дилеров на этой установке',
    other: '{count} дилера на этой установке',
  },
  'admin.colName': 'Название',
  'admin.colKey': 'Ключ',
  'admin.colStatus': 'Статус',
  'admin.colSchema': 'Схема',
  'admin.colInService': 'В работе',
  'admin.resume': 'Возобновить',
  'admin.suspend': 'Приостановить',

  'admin.setUpNote':
    'Будет создана база данных дилера, открыт учётный период на текущий месяц и заведён один руководитель, который затем добавит остальных. Вы никогда не увидите и не выберете их пароль.',
  'admin.dealershipName': 'Название дилера',
  'admin.shortName': 'Краткое имя',
  'admin.shortNameHint':
    'Строчные латинские буквы, цифры и дефисы. Сотрудники вводят его при входе, и изменить его потом нельзя.',
  'admin.firstLocation': 'Первая площадка',
  'admin.firstLocationPlaceholder': 'Основная площадка',
  'admin.locationCode': 'Код площадки',
  'admin.managerName': 'Имя руководителя',
  'admin.managerEmail': 'Почта руководителя',
  'admin.setItUp': 'Создать',
  'admin.settingItUp': 'Создание…',
  'admin.notCreated': 'Дилер не создан.',

  'admin.supportAccess': 'Доступ поддержки',
  'admin.supportEnter': 'Войти к дилеру',
  'admin.supportLede':
    'Вы сможете читать их записи и ничего не менять, не дольше часа. Это видно в их собственном журнале — с вашим именем и причиной, которую вы укажете здесь.',
  'admin.supportDealership': 'Дилер',
  'admin.supportReason': 'Зачем вам туда',
  'admin.supportReasonNote':
    'Запись сохраняется навсегда, и в их журнале, и в нашем. Пишите то, что готовы показать им самим.',
  'admin.supportOpen': 'Открыть доступ',
  'admin.supportOpening': 'Открытие…',
  'admin.supportOpened':
    'Вы внутри «{tenant}» до {time}. Откройте экраны дилера в этом браузере, чтобы посмотреть; закройте визит ниже, когда закончите.',
  'admin.supportRecord': 'Журнал',
  'admin.supportLoading': 'Загрузка журнала…',
  'admin.supportEmpty': 'Ещё никто не заходил к дилерам.',
  'admin.supportCaption': {
    one: '{count} визит поддержки',
    few: '{count} визита поддержки, начиная с последнего',
    many: '{count} визитов поддержки, начиная с последнего',
    other: '{count} визита поддержки, начиная с последнего',
  },
  'admin.supportColWho': 'Кто',
  'admin.supportColWhy': 'Причина',
  'admin.supportColOpened': 'Открыт',
  'admin.supportColState': 'Состояние',
  'admin.supportCloseNow': 'Закрыть сейчас',
  'admin.supportExpired': 'Истёк',
  'admin.supportClosed': 'Закрыт {date}',
};
