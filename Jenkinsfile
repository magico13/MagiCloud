pipeline {
    agent any
    environment {
        sdk = '.NET 6.0.100'
    }

    stages {
        stage('Prepare') {
            steps {
                echo 'Preparing.'
                dotnetRestore project: 'MagiCloud.sln', sdk: "${sdk}"
                withDotNet(sdk: "${sdk}") {
                    sh 'dotnet workload install wasm-tools'
                }
            }
        }
        stage('Build') {
            steps {
                echo 'Building..'
                dotnetPublish configuration: 'Release', project: 'MagiCloud.csproj', runtime: 'linux-x64', sdk: "${sdk}", selfContained: false, workDirectory: 'MagiCloud' 
                dotnetPublish configuration: 'Release', project: 'MagiCloudWeb.csproj', runtime: 'browser-wasm', sdk: "${sdk}", selfContained: true, workDirectory: 'MagiCloudWeb' 
                dotnetPublish configuration: 'Release', project: 'MagiConsole.csproj', sdk: "${sdk}", selfContained: false, workDirectory: 'MagiConsole' 
                dotnetPublish configuration: 'Release', project: 'MagiConsole.csproj', runtime: 'win-x64', sdk: "${sdk}", selfContained: true, workDirectory: 'MagiConsole'
                dotnetPublish configuration: 'Release', project: 'GogglesApi.csproj', runtime: 'linux-x64', sdk: "${sdk}", selfContained: false, workDirectory: 'GogglesApi'  
            }
        }
        stage('Test') {
            steps {
                echo 'Testing...'
            }
        }
        stage('Deploy') {
            steps {
                echo 'Deploying....'
                sh 'mv MagiCloud/bin/Release/net6.0/linux-x64/publish zMagiCloud'
                sh 'mv MagiCloudWeb/bin/Release/net6.0/publish/wwwroot zMagiCloud/wwwroot'
                sh 'cd zMagiCloud && zip -r MagiCloud.zip .'

                sh 'mv MagiConsole/bin/Release/net6.0/win-x64/publish zMagiConsoleWin'
                sh 'cd zMagiConsoleWin && zip -r MagiConsole-win.zip .'

                sh 'mv MagiConsole/bin/Release/net6.0/publish zMagiConsole'
                sh 'cd zMagiConsole && zip -r MagiConsole.zip .'

                sh 'mv GogglesApi/bin/Release/net6.0/linux-x64/publish zGogglesApi'
                sh 'cd zGogglesApi && zip -r GogglesApi.zip .'

                archiveArtifacts artifacts: '*/*.zip'
            }
        }
    }
}