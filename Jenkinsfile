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
                withDotNet(sdk: '.NET 6.0.100') {
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
                sh 'mv MagiCloudWeb/bin/Release/net6.0/publish zMagiCloud/wwwroot'
                sh 'zip -r MagiCloud.zip zMagiCloud'

                sh 'mv MagiConsole/bin/Release/net6.0/publish zMagiConsole'
                sh 'zip -r MagiConsole.zip zMagiConsole'
                
                archiveArtifacts artifacts: '*.zip'
            }
        }
    }
}