FROM php:8.5-apache@sha256:098cbb56b025956f6aec60e35c4e4c8c8dc15359b6a215de4868a9aec2092dce
RUN a2enmod rewrite
RUN rm -f /var/www/html/index.html
COPY version-check.php /var/www/html/version-check.php
COPY .htaccess /var/www/html/.htaccess
RUN chown -R www-data:www-data /var/www/html/
EXPOSE 80