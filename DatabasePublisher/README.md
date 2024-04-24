# Утилита для генерации скрипта обновления postgres базы данных

- аналог SQL Server проектов для Visual Studio
- для работы необходимо создать проект postgres базы данных, наполнить его скриптами создания таблиц, индексов и т.д.
- утилита создаёт временную базу на основе проекта и генерирует скрипт, приводящий целевую базу данных к состоянию временной

## Команда для сборки контейнера без встроенного postgres

- запускать из папки решения

`` docker build -t database-publisher:dev -f .\DatabasePublisher\Dockerfile --force-rm --target final . ``

*при использовании контейнера без встроенного postgres НЕОБХОДИМО указать параметр temp_connection со строкой подключение к серверу, на котором будет создана временная база*

## Команда для сборки контейнера со встроенным postgres


- запускать из папки решения

`` docker build -t database-publisher:dev -f .\DatabasePublisher\Dockerfile --force-rm --target final-with-postgres . ``

## Команда для использования

- запускать из папки с sql файлами(папка проекта базы данных), либо вместо `` ${PWD} `` указать путь к папке с sql файлами
- ${PWD} сработает только в PowerShell
- для генерации файла со скриптом публикации указывается параметр --generate_publish_file
- по умолчанию файл скрипта будет создан в папке bin/Debug/publish
- для указания папки скрипта публикации использовать параметр --output_directory
- если целевая база локальная, вместо localhost нужно использовать ``host.docker.internal``

`` docker run --rm -v ${PWD}:/working --network host database-publisher:dev --target_connection "{connection_string}" ``
