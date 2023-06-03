# Файл сборки для проекта GCodeCorrector

YBUILD_ADD_STANDARD_PROJECT(GCodeCorrector CSEXE)

add_dependencies(GCodeCorrector  YLocalization YMugenExtensions)