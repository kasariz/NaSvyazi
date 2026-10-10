/**
 * Анкета для тестировщиков игры «На связи» (версия 0.1.0).
 *
 * Как запустить (один раз, ~2 минуты):
 *   1. Откройте https://script.google.com → «Новый проект».
 *   2. Удалите всё в редакторе, вставьте этот файл целиком, нажмите «Сохранить».
 *   3. Вверху выберите функцию createFeedbackForm → «Выполнить».
 *      Google попросит разрешение создавать формы и таблицы — разрешите.
 *   4. Внизу, в «Журнале выполнения», появятся три ссылки:
 *      ссылка для тестировщиков, ссылка на редактирование и таблица с ответами.
 *
 * Форма анонимная: вход в Google для ответа не нужен, почта не собирается.
 * Ответы попадают в таблицу — её можно скачать как CSV для анализа.
 */
function createFeedbackForm() {
  var VERSION = '0.1.0';

  var form = FormApp.create('«На связи» — отзыв о версии ' + VERSION);
  form.setDescription(
    'Спасибо, что поиграли! Анкета анонимная и займёт около 5 минут. ' +
    'Ответы помогут исправить ошибки и войдут в выпускную квалификационную работу ' +
    '(глава о тестировании прототипа). Если что-то не помните — пропускайте необязательные вопросы.'
  );
  form.setCollectEmail(false);
  form.setLimitOneResponsePerUser(false); // иначе потребуется вход в Google
  form.setProgressBar(true);
  form.setConfirmationMessage('Спасибо! Ваш отзыв записан. Следующая версия будет лучше благодаря вам.');

  var LIKERT = ['1 — совсем не согласен', '2', '3', '4', '5 — полностью согласен'];
  var RATING = ['1 — плохо', '2', '3', '4', '5 — отлично', 'Не пробовал(а)'];
  var FEATURES = [
    'Касса: сканирование, оплата, сдача',
    'Консультации по тарифам',
    'Закупка и выкладка товара',
    'Цены и реакция покупателей',
    'Мебель и её расстановка',
    'Расширение зала и склада',
    'Дерево навыков',
    'События дня («Чёрная пятница» и др.)',
    'Воришки и камеры',
    'Оформление салона'
  ];

  // ---------- 1. О вас ----------
  form.addSectionHeaderItem().setTitle('О вас');

  form.addMultipleChoiceItem()
    .setTitle('Возраст')
    .setChoiceValues(['18–24', '25–34', '35–44', '45 и старше'])
    .setRequired(true);

  form.addMultipleChoiceItem()
    .setTitle('Как часто вы играете в компьютерные игры?')
    .setChoiceValues(['Каждый день', 'Несколько раз в неделю', 'Иногда', 'Почти не играю'])
    .setRequired(true);

  form.addMultipleChoiceItem()
    .setTitle('Играли ли вы раньше в симуляторы магазина (Supermarket Simulator и похожие)?')
    .setChoiceValues(['Да', 'Нет'])
    .setRequired(true);

  form.addMultipleChoiceItem()
    .setTitle('Работали ли вы в салоне связи?')
    .setChoiceValues(['Да, работаю или работал(а)', 'Нет'])
    .setRequired(true);

  // ---------- 2. Как прошла игра ----------
  form.addPageBreakItem().setTitle('Как прошла игра');

  form.addMultipleChoiceItem()
    .setTitle('Сколько времени вы играли?')
    .setChoiceValues(['Меньше 15 минут', '15–30 минут', '30–60 минут', 'Больше часа'])
    .setRequired(true);

  form.addMultipleChoiceItem()
    .setTitle('До какого игрового дня дошли?')
    .setChoiceValues(['1–2', '3–5', '6–10', 'Больше 10'])
    .setRequired(true);

  form.addMultipleChoiceItem()
    .setTitle('Помогло ли обучение в начале игры?')
    .setChoiceValues(['Да, всё стало понятно', 'Частично', 'Нет, многое осталось непонятным', 'Пропустил(а) обучение'])
    .setRequired(true);

  form.addMultipleChoiceItem()
    .setTitle('Как игра работала на вашем компьютере?')
    .setChoiceValues(['Всё плавно', 'Иногда подтормаживала', 'Сильно тормозила', 'Вылетала или не запускалась'])
    .setRequired(true);

  form.addTextItem()
    .setTitle('Процессор и видеокарта (если знаете)')
    .setHelpText('Например: Ryzen 5 5600, GTX 1660. Нужно, чтобы понять, на каком железе есть проблемы.');

  // ---------- 3. Удобство (шкала SUS) ----------
  form.addPageBreakItem()
    .setTitle('Удобство игры')
    .setHelpText('Оцените, насколько вы согласны с каждым утверждением. ' +
                 'Это стандартная шкала удобства SUS — отвечайте первым впечатлением.');

  form.addGridItem()
    .setTitle('Насколько вы согласны с утверждениями?')
    .setRows([
      '1. Я хотел(а) бы играть в эту игру регулярно',
      '2. Игра показалась мне излишне сложной',
      '3. Мне было легко разобраться в игре',
      '4. Думаю, мне понадобилась бы помощь, чтобы разобраться в игре',
      '5. Разные возможности игры хорошо связаны между собой',
      '6. В игре слишком много несогласованного и нелогичного',
      '7. Думаю, большинство людей быстро научатся играть в эту игру',
      '8. Играть было неудобно и громоздко',
      '9. Я чувствовал(а) себя уверенно во время игры',
      '10. Мне пришлось многому научиться, прежде чем играть нормально'
    ])
    .setColumns(LIKERT)
    .setRequired(true);

  // ---------- 4. Оценка частей игры ----------
  form.addPageBreakItem().setTitle('Оценка частей игры');

  form.addGridItem()
    .setTitle('Оцените части игры')
    .setRows([
      'Управление (ходьба, взаимодействие)',
      'Понятность обучения и подсказок',
      'Интерфейс терминала (товары, мебель, развитие)',
      'Касса и выдача сдачи',
      'Консультации по тарифам',
      'Экономика и баланс (цены, аренда, заработок)',
      'Длительность игрового дня',
      'Графика и оформление салона',
      'Звук и музыка'
    ])
    .setColumns(RATING)
    .setRequired(true);

  form.addScaleItem()
    .setTitle('Насколько игра похожа на работу настоящего салона связи?')
    .setBounds(1, 5)
    .setLabels('Совсем не похожа', 'Очень похожа');

  form.addCheckboxItem()
    .setTitle('Что понравилось больше всего?')
    .setChoiceValues(FEATURES)
    .showOtherOption(true);

  form.addCheckboxItem()
    .setTitle('Что понравилось меньше всего?')
    .setChoiceValues(FEATURES)
    .showOtherOption(true);

  // ---------- 5. Ошибки ----------
  form.addPageBreakItem().setTitle('Ошибки');

  form.addMultipleChoiceItem()
    .setTitle('Встречались ли ошибки?')
    .setChoiceValues(['Нет', 'Да, мелкие', 'Да, мешали играть', 'Да, игра зависла или вылетела'])
    .setRequired(true);

  form.addParagraphTextItem()
    .setTitle('Опишите ошибки')
    .setHelpText('Что делали, что произошло, что ожидали увидеть. Чем подробнее, тем быстрее исправлю.');

  // ---------- 6. Итог ----------
  form.addPageBreakItem().setTitle('Итог');

  form.addScaleItem()
    .setTitle('Общая оценка игры')
    .setBounds(1, 10)
    .setLabels('Очень плохо', 'Отлично')
    .setRequired(true);

  form.addScaleItem()
    .setTitle('Насколько вероятно, что вы порекомендуете игру знакомым?')
    .setBounds(0, 10)
    .setLabels('Точно нет', 'Точно да')
    .setRequired(true);

  form.addMultipleChoiceItem()
    .setTitle('Если игра выйдет в магазине, вы бы в неё сыграли?')
    .setChoiceValues(['Да, даже за деньги', 'Да, если бесплатно', 'Скорее нет', 'Нет'])
    .setRequired(true);

  form.addParagraphTextItem().setTitle('Что бы вы добавили в игру?');
  form.addParagraphTextItem().setTitle('Что раздражало больше всего?');
  form.addParagraphTextItem().setTitle('Любые другие комментарии');

  // ---------- Таблица с ответами ----------
  var sheet = SpreadsheetApp.create('«На связи» — ответы тестировщиков ' + VERSION);
  form.setDestination(FormApp.DestinationType.SPREADSHEET, sheet.getId());

  Logger.log('Ссылка для тестировщиков: ' + form.getPublishedUrl());
  Logger.log('Редактирование формы:     ' + form.getEditUrl());
  Logger.log('Таблица с ответами:       ' + sheet.getUrl());
}
