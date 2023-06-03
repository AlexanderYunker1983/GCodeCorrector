# Файл сборки инсталляторов GCodeCorrector
set(Y_INSTALLER_SCRIPT "GCodeCorrector")

YBUILD_ADD_INSTALLER_NSIS(
        Installer
        "${Y_INSTALLER_SCRIPT}"
        BOOTSTRAPPER_SCRIPT
            bootstrapper.nsi)
        
add_dependencies(Installer GCodeCorrector)
