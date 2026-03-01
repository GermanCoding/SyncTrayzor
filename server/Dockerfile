FROM php:8.5-apache@sha256:9be84c47f2791d429a3fd82beee8109be123feb093e6c428269aa311ef1d3190
RUN a2enmod rewrite
RUN rm -f /var/www/html/index.html
COPY version-check.php /var/www/html/version-check.php
COPY .htaccess /var/www/html/.htaccess
RUN chown -R www-data:www-data /var/www/html/
EXPOSE 80