FROM php:8.5-apache@sha256:30adf7fe5a8d44dae7737760d4c76b4ebd6f5e8bcc30334bb1370b2ac7771422
RUN a2enmod rewrite
RUN rm -f /var/www/html/index.html
COPY version-check.php /var/www/html/version-check.php
COPY .htaccess /var/www/html/.htaccess
RUN chown -R www-data:www-data /var/www/html/
EXPOSE 80