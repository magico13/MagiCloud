pipeline {
    agent any
    environment {
        sdk = '.NET 6.0'
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
                dotnetPublish configuration: 'Release', project: 'GogglesApi.csproj', runtime: 'linux-x64', sdk: "${sdk}", selfContained: false, workDirectory: 'GogglesApi'  
            }
        }
        stage('Test') {
            steps {
                echo 'Testing...'
                dotnetTest configuration: 'Release', logger: 'trx', project: 'MagiCloud.sln', sdk: "${sdk}"
            }
        }
        stage('Deploy') {
            steps {
                echo 'Deploying....'
                sh 'mv MagiCloud/bin/Release/net6.0/linux-x64/publish zMagiCloud'
                sh 'cd zMagiCloud && zip -r MagiCloud.zip .'

                sh 'mv GogglesApi/bin/Release/net6.0/linux-x64/publish zGogglesApi'
                sh 'cd zGogglesApi && zip -r GogglesApi.zip .'

                archiveArtifacts artifacts: '*/*.zip'
            }
        }
    }
}